using PKForge.Infrastructure;
using PKForge.Engine;
using PKForge.Domain;
using Xunit;

namespace PKForge.Engine.Tests;

public sealed class DesktopFileTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "PKForge-tests-" + Guid.NewGuid().ToString("N"));
    public DesktopFileTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public async Task ReplacingSaveTruncatesItAndLeavesNoTemporaryFiles()
    {
        var file = Path.Combine(_directory, "тест.sav");
        await File.WriteAllBytesAsync(file, new byte[4096]);
        var access = new LocalSaveFileAccess();
        await access.WriteAtomicallyAsync(file, new byte[] { 4, 5, 6 });
        Assert.Equal(new byte[] { 4, 5, 6 }, (await access.ReadAsync(file)).ToArray());
        Assert.Single(Directory.GetFiles(_directory));
    }

    [Fact]
    public async Task CancellationKeepsOriginalSave()
    {
        var file = Path.Combine(_directory, "main");
        await File.WriteAllBytesAsync(file, new byte[] { 1, 2, 3 });
        using var token = new CancellationTokenSource();
        token.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new LocalSaveFileAccess().WriteAtomicallyAsync(file, new byte[] { 9 }, token.Token).AsTask());
        Assert.Equal(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(file));
        Assert.Single(Directory.GetFiles(_directory));
    }

    [Fact]
    public async Task LockedSaveCannotBeOverwritten()
    {
        if (!OperatingSystem.IsWindows()) return;
        var file = Path.Combine(_directory, "main");
        await File.WriteAllBytesAsync(file, new byte[] { 1, 2, 3 });
        using (var locked = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var error = await Record.ExceptionAsync(() =>
                new LocalSaveFileAccess().WriteAtomicallyAsync(file, new byte[] { 9 }).AsTask());
            Assert.True(error is IOException or UnauthorizedAccessException, error?.ToString() ?? "A locked save was overwritten.");
        }
        Assert.Equal(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(file));
        Assert.Single(Directory.GetFiles(_directory));
    }

    [Theory]
    [InlineData("../outside.sav")]
    [InlineData("..\\outside.sav")]
    [InlineData("G:\\outside.sav")]
    [InlineData("main:stream")]
    [InlineData("..")]
    public async Task FolderExportsRejectPaths(string name) =>
        await Assert.ThrowsAsync<ArgumentException>(() =>
            new LocalFolderFileAccess().WriteFileAsync(_directory, name, new byte[] { 9 }).AsTask());

    [Fact]
    public async Task ScannerFindsRealSavesInNestedFoldersAndRejectsInvalidBytes()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "PKForge.sln"))) root = root.Parent;
        Assert.NotNull(root);
        var fixture = Path.Combine(root!.FullName, "external/PKHeX/Tests/PKHeX.Core.Tests/TestData/SM Project 802.main");
        var nested = Directory.CreateDirectory(Path.Combine(_directory, "сохранения"));
        var file = Path.Combine(nested.FullName, "main");
        File.Copy(fixture, file);
        await File.WriteAllBytesAsync(Path.Combine(_directory, "invalid.sav"), new byte[] { 1, 2, 3 });
        await File.WriteAllBytesAsync(Path.Combine(_directory, "rom.gba"), new byte[] { 1, 2, 3 });
        var result = await new LocalEmulatorScanner(new SaveEngine()).ScanAsync(_directory, EmulatorKind.Azahar);
        var save = Assert.Single(result.Saves);
        Assert.Equal(file, save.DocumentId);
        Assert.Equal(7, save.Generation);
        Assert.True(save.RequiresExtraCare);
        Assert.Equal(3, result.FilesSeen);
        Assert.Single(result.RejectedCandidates);
        Assert.Equal(await File.ReadAllBytesAsync(fixture), await File.ReadAllBytesAsync(file));
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
