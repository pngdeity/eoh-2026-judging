namespace ContestJudging.Services.Managers;

using Blazored.LocalStorage;

using ContestJudging.Core.Interfaces;

using Microsoft.Extensions.Logging;

public sealed class BackupService : IBackupService
{
    private readonly ILocalStorageService _localStorage;
    private readonly IDatabaseBackupService _dbBackup;
    private readonly ILogger<BackupService> _logger;
    private const int CurrentSchemaVersion = 1;

    // Integrity protections:
    // 1. Schema version check — discards backups from different app versions.
    // 2. DatabaseBackupService.ImportAsync validates SQLite magic bytes and minimum file size.
    // 3. Base64 decode failure surfaces as a caught exception (no silent corruption).
    // Combined, these provide sufficient integrity verification for a client-side backup.

    public BackupService(ILocalStorageService localStorage, IDatabaseBackupService dbBackup, ILogger<BackupService> logger)
    {
        _localStorage = localStorage;
        _dbBackup = dbBackup;
        _logger = logger;
    }

    public async Task SaveBackupAsync(byte[] dbData)
    {
        var base64 = Convert.ToBase64String(dbData);
        if (base64.Length > 5 * 1024 * 1024)
        {
            _logger.LogWarning("Database backup exceeds localStorage limit: {Size}MB", base64.Length / (1024.0 * 1024.0));
            return;
        }
        await _localStorage.SetItemAsync("db_backup", base64);
        await _localStorage.SetItemAsync("db_schema_version", CurrentSchemaVersion);
    }

    public async Task<byte[]?> TryRestoreBackupAsync()
    {
        if (!await _localStorage.ContainKeyAsync("db_backup"))
            return null;

        var storedVersion = await _localStorage.GetItemAsync<int>("db_schema_version");
        if (storedVersion != default && storedVersion != CurrentSchemaVersion)
        {
            _logger.LogWarning("Schema version mismatch: stored={Stored}, current={Current}. Discarding backup.",
                storedVersion, CurrentSchemaVersion);
            await _localStorage.RemoveItemAsync("db_backup");
            await _localStorage.RemoveItemAsync("db_schema_version");
            return null;
        }

        var base64 = await _localStorage.GetItemAsync<string>("db_backup");
        if (string.IsNullOrEmpty(base64))
            return null;

        try
        {
            var data = Convert.FromBase64String(base64);
            await _dbBackup.ImportAsync(data);
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
