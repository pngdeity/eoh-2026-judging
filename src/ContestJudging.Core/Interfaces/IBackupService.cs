namespace ContestJudging.Core.Interfaces;

public interface IBackupService
{
    Task SaveBackupAsync(byte[] dbData);
    Task<byte[]?> TryRestoreBackupAsync();
}
