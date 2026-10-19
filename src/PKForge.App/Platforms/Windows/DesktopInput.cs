using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using PKForge.App.Services;
using Windows.System;

namespace PKForge.App.Platforms.Windows;

internal static class DesktopInput
{
    internal static void Attach(Microsoft.Maui.Controls.Window window)
    {
        if (window.Handler?.PlatformView is not Microsoft.UI.Xaml.Window native || native.Content is not UIElement root) return;
        var router = IPlatformApplication.Current?.Services.GetRequiredService<GamepadRouter>();
        root.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler((_, e) =>
        {
            var editing = FocusManager.GetFocusedElement(root.XamlRoot) is TextBox or PasswordBox or AutoSuggestBox;
            if (editing && e.Key != VirtualKey.Escape) return;
            if (Map(e.Key) is { } button) e.Handled = router?.Dispatch(button) == true;
        }), handledEventsToo: true);
        root.AddHandler(UIElement.KeyUpEvent, new KeyEventHandler((_, e) =>
        {
            if (Map(e.Key) is { } button) router?.DispatchRelease(button);
        }), handledEventsToo: true);
    }

    private static PadButton? Map(VirtualKey key) => key switch
    {
        VirtualKey.Enter => PadButton.A,
        VirtualKey.Escape => PadButton.B,
        VirtualKey.X => PadButton.X,
        VirtualKey.Y => PadButton.Y,
        VirtualKey.F10 => PadButton.Start,
        VirtualKey.F2 => PadButton.Select,
        VirtualKey.PageUp => PadButton.L,
        VirtualKey.PageDown => PadButton.R,
        VirtualKey.Up => PadButton.Up,
        VirtualKey.Down => PadButton.Down,
        VirtualKey.Left => PadButton.Left,
        VirtualKey.Right => PadButton.Right,
        _ => null,
    };
}
