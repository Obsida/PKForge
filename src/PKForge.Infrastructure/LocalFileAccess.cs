using PKForge.Domain;

namespace PKForge.Infrastructure;

/// <summary>Desktop file access. Replaces a file only after the entire new image is flushed.</summary>
public sealed class LocalSaveFileAccess : ISaveFileAccess
{
    public async ValueTask<ReadOnlyMemory<byte>> ReadAsync(string documentId, CancellationToken cancellationToken = default) =>
        await File.ReadAllBytesAsync(Path.GetFullPath(documentId), cancellationToken).ConfigureAwait(false);

    public async ValueTask WriteAtomicallyAsync(string documentId, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default)
    {
        var destination = Path.GetFullPath(documentId);
        var temporary = Path.Combine(Path.GetDirectoryName(destination)!, $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                65536, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            // A same-directory rename keeps the swap on the same volume. The caller's
            // SafeSaveWriter has already validated the image and made its restore point.
            File.Move(temporary, destination, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}

public sealed class LocalFolderFileAccess : IFolderFileAccess
{
    private readonly LocalSaveFileAccess _files = new();

    public ValueTask<IReadOnlyList<PickedDocument>> ListFilesAsync(string treeId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<PickedDocument> files = Directory.EnumerateFiles(Path.GetFullPath(treeId))
            .Select(path => new PickedDocument(path, Path.GetFileName(path))).ToArray();
        return ValueTask.FromResult(files);
    }

    public ValueTask<ReadOnlyMemory<byte>> ReadFileAsync(string documentId, CancellationToken cancellationToken = default) =>
        _files.ReadAsync(documentId, cancellationToken);

    public ValueTask WriteFileAsync(string treeId, string fileName, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName is "." or ".." ||
            fileName.IndexOfAny(['/', '\\', ':']) >= 0 || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("Use a file name without a directory path.", nameof(fileName));
        return _files.WriteAtomicallyAsync(Path.Combine(Path.GetFullPath(treeId), fileName), bytes, cancellationToken);
    }
}
