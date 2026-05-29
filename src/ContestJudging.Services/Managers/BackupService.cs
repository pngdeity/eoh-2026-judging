namespace ContestJudging.Services.Managers;

using Blazored.LocalStorage;

using ContestJudging.Core;
using ContestJudging.Core.Interfaces;

using Microsoft.Extensions.Logging;

public sealed class BackupService : IBackupService
{
    private readonly ILocalStorageService _localStorage;
    private readonly IDatabaseBackupService _dbBackup;
    private readonly ILogger<BackupService> _logger;
    private const int CurrentSchemaVersion = 1;

    public BackupService(ILocalStorageService localStorage, IDatabaseBackupService dbBackup, ILogger<BackupService> logger)
    {
        _localStorage = localStorage;
        _dbBackup = dbBackup;
        _logger = logger;
    }

    public async Task SaveBackupAsync(byte[] dbData)
    {
        var base64 = Convert.ToBase64String(dbData);
        if (base64.Length > Constants.MaxBackupSizeBytes)
        {
            _logger.LogWarning("Database backup exceeds localStorage limit: {Size}MB", base64.Length / (1024.0 * 1024.0));
            return;
        }
        await _localStorage.SetItemAsync(Constants.BackupStorageKey, base64).ConfigureAwait(false);
        await _localStorage.SetItemAsync(Constants.SchemaVersionStorageKey, CurrentSchemaVersion).ConfigureAwait(false);
    }

    public async Task<byte[]?> TryRestoreBackupAsync()
    {
        if (!await _localStorage.ContainKeyAsync(Constants.BackupStorageKey))
            return null;

        var storedVersion = await _localStorage.GetItemAsync<int>(Constants.SchemaVersionStorageKey).ConfigureAwait(false);
        if (storedVersion != CurrentSchemaVersion)
        {
            _logger.LogWarning("Schema version mismatch: stored={Stored}, current={Current}. Discarding backup.",
                storedVersion, CurrentSchemaVersion);
            await _localStorage.RemoveItemAsync(Constants.BackupStorageKey).ConfigureAwait(false);
            await _localStorage.RemoveItemAsync(Constants.SchemaVersionStorageKey).ConfigureAwait(false);
            return null;
        }

        var base64 = await _localStorage.GetItemAsync<string>(Constants.BackupStorageKey).ConfigureAwait(false);
        if (string.IsNullOrEmpty(base64))
            return null;

        try
        {
            var data = Convert.FromBase64String(base64);
            await _dbBackup.ImportAsync(data).ConfigureAwait(false);
            _logger.LogInformation("Database restored from backup");
            return data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore database from backup");
            return null;
        }
    }
}
