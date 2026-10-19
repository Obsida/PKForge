using PKForge.Domain;
using Windows.Storage.Pickers;

namespace PKForge.App;

internal static class WindowsPickerOwner
{
    internal static void Initialize(object picker)
    {
        var window = Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView
            ?? throw new InvalidOperationException("The application window is unavailable.");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(window));
    }
}

public sealed class WindowsDocumentPicker : IDocumentPicker
{
    private static FileOpenPicker Create()
    {
        var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
        picker.FileTypeFilter.Add("*");
        WindowsPickerOwner.Initialize(picker);
        return picker;
    }

    public async ValueTask<PickedDocument?> PickSaveAsync(CancellationToken cancellationToken = default)
    {
        var file = await Create().PickSingleFileAsync().AsTask(cancellationToken);
        return file is null ? null : new PickedDocument(file.Path, file.Name);
    }

    public async ValueTask<IReadOnlyList<PickedDocument>> PickManyAsync(CancellationToken cancellationToken = default)
    {
        var files = await Create().PickMultipleFilesAsync().AsTask(cancellationToken);
        return files.Select(file => new PickedDocument(file.Path, file.Name)).ToArray();
    }
}

public sealed class WindowsFolderPicker : IFolderPicker
{
    public async ValueTask<PickedFolder?> PickFolderAsync(CancellationToken cancellationToken = default)
    {
        var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
        picker.FileTypeFilter.Add("*");
        WindowsPickerOwner.Initialize(picker);
        var folder = await picker.PickSingleFolderAsync().AsTask(cancellationToken);
        return folder is null ? null : new PickedFolder(folder.Path, folder.DisplayName);
    }
}

/// <summary>Desktop shows the inspector in its main window.</summary>
public sealed class DesktopSecondaryDisplayHost : ISecondaryDisplayHost
{
    public bool IsAvailable => false;
    public ValueTask ShowAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    public ValueTask DismissAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
}
