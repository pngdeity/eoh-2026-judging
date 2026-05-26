using System.Threading;

namespace ContestJudging.Infrastructure.Persistence;

using ContestJudging.Core.Interfaces;

public sealed class DatabaseBackupService : IDatabaseBackupService
{
    // Database path is hardcoded — client-side WASM app with no config file support.
    // SQLite is embedded in the browser; the path is safe.
    private readonly string _dbPath;

    public DatabaseBackupService(string dbPath = "contest.db")
    {
        _dbPath = dbPath;
    }

    public async Task<byte[]> ExportAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(_dbPath))
            return await File.ReadAllBytesAsync(_dbPath, cancellationToken);
        return Array.Empty<byte>();
    }

    public async Task ImportAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        if (data == null || data.Length < 16)
            throw new ArgumentException("Invalid database file");
        // Minimum valid SQLite file: 100-byte header
        if (data.Length < 100)
            throw new ArgumentException("Database file is too small to be a valid SQLite database");
        var magic = "SQLite format 3\0"u8;
        for (int i = 0; i < 16; i++)
        {
            if (data[i] != magic[i])
                throw new ArgumentException("Not a valid SQLite database file");
        }
        await File.WriteAllBytesAsync(_dbPath, data, cancellationToken);
    }
}
