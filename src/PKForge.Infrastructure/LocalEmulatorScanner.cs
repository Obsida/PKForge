using PKForge.Domain;

namespace PKForge.Infrastructure;

/// <summary>Finds battery saves on a PC. File names are hints; the engine must recognize every result.</summary>
public sealed class LocalEmulatorScanner(ISaveEngine engine) : IEmulatorDetectionService
{
    public ValueTask<EmulatorScanResult> ScanAsync(string treeId, EmulatorKind kind, CancellationToken cancellationToken = default) =>
        new(Task.Run(() => Scan(treeId, kind, cancellationToken), cancellationToken));

    private EmulatorScanResult Scan(string treeId, EmulatorKind kind, CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(treeId);
        var found = new List<DetectedSave>();
        var rejected = new List<string>();
        var diagnostics = new List<string>();
        var seen = 0;
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            MaxRecursionDepth = 8,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
        };
        foreach (var path in Directory.EnumerateFiles(root, "*", options))
        {
            cancellationToken.ThrowIfCancellationRequested();
            seen++;
            var name = Path.GetFileName(path);
            if (!EmulatorSaveHeuristics.IsCandidateFileName(name, kind)) continue;
            try
            {
                var info = new FileInfo(path);
                if (info.Length is <= 0 or > 32 * 1024 * 1024) continue;
                // Sharing Read only prevents reading an emulator's half-written save.
                using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (input.Length is <= 0 or > 32 * 1024 * 1024) continue;
                var bytes = new byte[checked((int)input.Length)];
                input.ReadExactly(bytes);
                var description = engine.TryDescribe(bytes, name);
                if (description is null) { rejected.Add(path); continue; }
                var siblings = Directory.EnumerateFiles(info.DirectoryName!).Select(Path.GetFileName).OfType<string>();
                var rom = EmulatorSaveHeuristics.FindSiblingRom(name, siblings);
                var hint = EmulatorSaveHeuristics.GuessSwitchGameLabel(path);
                var guess = SaveIdentityRules.Guess(description.GameName, description.Generation, name, rom, hint);
                found.Add(new DetectedSave(path, name, guess.Label, kind,
                    EmulatorSaveHeuristics.RequiresExtraCare(kind), info.LastWriteTimeUtc,
                    description.Generation, description.TrainerName, description.PlayTime,
                    guess, rom, EmulatorSaveHeuristics.FolderHint(path), Language: description.Language));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                if (diagnostics.Count < 100) diagnostics.Add($"{path}: {error.Message}");
            }
        }
        return new EmulatorScanResult(EmulatorSaveHeuristics.Normalize(found), seen, rejected, diagnostics);
    }
}
