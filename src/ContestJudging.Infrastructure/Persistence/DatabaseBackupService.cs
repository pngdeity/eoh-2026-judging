using System.Threading;

using ContestJudging.Core;
using ContestJudging.Core.Interfaces;

namespace ContestJudging.Infrastructure.Persistence;

public sealed class DatabaseBackupService : IDatabaseBackupService
{
    private readonly string _dbPath;

    public DatabaseBackupService(string dbPath = Constants.DatabaseFileName)
    {
        _dbPath = dbPath;
    }

    public async Task<byte[]> ExportAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(_dbPath))
            return await File.ReadAllBytesAsync(_dbPath, cancellationToken).ConfigureAwait(false);
        return Array.Empty<byte>();
    }

    public async Task ImportAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        if (data == null || data.Length < Constants.SqliteHeaderLength)
            throw new ArgumentException("Invalid database file");
        if (data.Length < Constants.MinimumDatabaseFileSize)
            throw new ArgumentException("Database file is too small to be a valid SQLite database");
        var magic = "SQLite format 3\0"u8;
        for (int i = 0; i < Constants.SqliteHeaderLength; i++)
        {
            if (data[i] != magic[i])
                throw new ArgumentException("Not a valid SQLite database file");
        }
        await File.WriteAllBytesAsync(_dbPath, data, cancellationToken).ConfigureAwait(false);
    }
}
