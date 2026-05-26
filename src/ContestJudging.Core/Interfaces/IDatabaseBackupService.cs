using System.Threading;

namespace ContestJudging.Core.Interfaces;

public interface IDatabaseBackupService
{
    Task<byte[]> ExportAsync(CancellationToken cancellationToken = default);
    Task ImportAsync(byte[] data, CancellationToken cancellationToken = default);
}
