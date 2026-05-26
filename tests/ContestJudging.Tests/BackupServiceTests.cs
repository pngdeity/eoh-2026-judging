using Blazored.LocalStorage;

using ContestJudging.Core.Interfaces;
using ContestJudging.Services.Managers;

using Microsoft.Extensions.Logging;

using Moq;

using Xunit;

namespace ContestJudging.Tests;

[Trait("Category", "Unit")]
[Trait("Category", "Unit")]
public class BackupServiceTests
{
    private readonly Mock<ILocalStorageService> _mockStorage;
    private readonly Mock<IDatabaseBackupService> _mockDbBackup;
    private readonly Mock<ILogger<BackupService>> _mockLogger;
    private readonly BackupService _service;

    public BackupServiceTests()
    {
        _mockStorage = new Mock<ILocalStorageService>();
        _mockDbBackup = new Mock<IDatabaseBackupService>();
        _mockLogger = new Mock<ILogger<BackupService>>();
        _service = new BackupService(_mockStorage.Object, _mockDbBackup.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task SaveBackupAsync_StoresBase64AndVersion()
    {
        var data = new byte[] { 1, 2, 3 };
        await _service.SaveBackupAsync(data);

        _mockStorage.Verify(s => s.SetItemAsync("db_backup", Convert.ToBase64String(data)), Times.Once);
        _mockStorage.Verify(s => s.SetItemAsync("db_schema_version", 1), Times.Once);
    }

    [Fact]
    public async Task TryRestoreBackupAsync_NoBackup_ReturnsNull()
    {
        _mockStorage.Setup(s => s.ContainKeyAsync("db_backup")).ReturnsAsync(false);
        var result = await _service.TryRestoreBackupAsync();
        Assert.Null(result);
    }

    [Fact]
    public async Task TryRestoreBackupAsync_VersionMismatch_ReturnsNull()
    {
        _mockStorage.Setup(s => s.ContainKeyAsync("db_backup")).ReturnsAsync(true);
        _mockStorage.Setup(s => s.GetItemAsync<int>("db_schema_version")).ReturnsAsync(999);
        var result = await _service.TryRestoreBackupAsync();
        Assert.Null(result);
        _mockStorage.Verify(s => s.RemoveItemAsync("db_backup"), Times.Once);
    }

    [Fact]
    public async Task TryRestoreBackupAsync_ValidBackup_Restores()
    {
        var expectedData = new byte[] { 1, 2, 3 };
        _mockStorage.Setup(s => s.ContainKeyAsync("db_backup")).ReturnsAsync(true);
        _mockStorage.Setup(s => s.GetItemAsync<int>("db_schema_version")).ReturnsAsync(1);
        _mockStorage.Setup(s => s.GetItemAsync<string>("db_backup")).ReturnsAsync(Convert.ToBase64String(expectedData));

        var result = await _service.TryRestoreBackupAsync();

        Assert.NotNull(result);
        Assert.Equal(expectedData, result);
        _mockDbBackup.Verify(b => b.ImportAsync(expectedData), Times.Once);
    }

    [Fact]
    public async Task TryRestoreBackupAsync_CorruptBase64_ReturnsNull()
    {
        _mockStorage.Setup(s => s.ContainKeyAsync("db_backup")).ReturnsAsync(true);
        _mockStorage.Setup(s => s.GetItemAsync<int>("db_schema_version")).ReturnsAsync(1);
        _mockStorage.Setup(s => s.GetItemAsync<string>("db_backup")).ReturnsAsync("not-valid-base64!!!");

        var result = await _service.TryRestoreBackupAsync();

        Assert.Null(result);
    }
}
