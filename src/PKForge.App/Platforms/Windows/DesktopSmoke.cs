#if DIAGNOSTIC
using System.Reflection;
using PKForge.App.Services;
using PKForge.App.Views;
using PKForge.App.ViewModels;
using PKForge.Domain;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace PKForge.App.Platforms.Windows;

// Development-only exercise of the real screens, on the app's UI thread.
internal static class DesktopSmoke
{
    private static bool _started;
    internal static void Start(Microsoft.Maui.Controls.Window window)
    {
        if (_started || Environment.GetEnvironmentVariable("PKFORGE_SMOKE") is not { Length: > 0 } mode) return;
        _started = true;
        _ = RunAsync(window, mode);
    }

    private static void Log(string message)
    {
        var folder = Environment.GetEnvironmentVariable("PKFORGE_SMOKE_OUTPUT")!;
        Directory.CreateDirectory(folder);
        File.AppendAllText(Path.Combine(folder, "smoke.log"), $"{DateTime.Now:O} {message}\n");
    }

    private static async Task RunAsync(Microsoft.Maui.Controls.Window window, string mode)
    {
        try
        {
            await Task.Delay(2000);
            var navigation = (NavigationPage)window.Page!;
            var home = navigation.CurrentPage;
            var router = IPlatformApplication.Current!.Services.GetRequiredService<GamepadRouter>();
            Log($"Starting {mode}");
            if (mode == "events")
            {
                var task = (Task)typeof(HomePage).GetMethod("ShowEventsMenuAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(home, null)!;
                await Task.Delay(2000);
                Log("Events menu rendered");
                router.Dispatch(PadButton.A);
                await Task.Delay(4000);
                Log("Community catalogue opened (or network error displayed)");
                await SnapshotAsync(window, "events");
                router.Dispatch(PadButton.B);
                await task;
            }
            else if (mode == "park")
            {
                await (Task)typeof(HomePage).GetMethod("PushParkAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(home, null)!;
                await Task.Delay(5000);
                Log("Park rendered");
                await SnapshotAsync(window, "park");
                await navigation.PopAsync();
                await Task.Delay(500);
            }
            else if (mode == "theme")
            {
                var host = (Grid)typeof(HomePage).GetField("_hostGrid", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(home)!;
                var task = ThemePicker.ShowAsync(host);
                await Task.Delay(1000);
                var picker = ((IEnumerable<IPadHandler>)typeof(GamepadRouter).GetField("_stack", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(router)!).OfType<ThemePicker>().Single();
                var canvas = (SKCanvasView)typeof(ThemePicker).GetField("_canvas", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(picker)!;
                var touch = typeof(ThemePicker).GetMethod("Touch", BindingFlags.Instance | BindingFlags.NonPublic)!;
                touch.Invoke(picker, [canvas, new SKTouchEventArgs(1, SKTouchAction.WheelChanged, SKMouseButton.Unknown, SKTouchDeviceType.Mouse, new SKPoint(100, 100), false, -600)]);
                await Task.Delay(800);
                var scroll = (Spring)typeof(ThemePicker).GetField("_scroll", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(picker)!;
                if (scroll.Value < 1) throw new InvalidOperationException("Mouse wheel did not scroll");
                Log($"Wheel scroll: {scroll.Value}");
                await Task.Delay(1500);
                if (!scroll.Settled) throw new InvalidOperationException("Scroll animation never settled");
                await SnapshotAsync(window, "theme-scrolled");
                router.Dispatch(PadButton.L);
                await Task.Delay(500);
                router.Dispatch(PadButton.B);
                await task;
            }
            else if (mode is "stats" or "gifts")
            {
                var services = IPlatformApplication.Current!.Services;
                var path = Path.Combine(Environment.GetEnvironmentVariable("PKFORGE_SMOKE_OUTPUT")!, "main");
                File.Copy(Environment.GetEnvironmentVariable("PKFORGE_SMOKE_SAVE")!, path, true);
                var sessions = services.GetRequiredService<ISaveSessionService>();
                await sessions.OpenAsync(new PickedDocument(path, "main"));
                var session = sessions.CurrentSession!;
                var vm = services.GetRequiredService<BoxBrowserViewModel>();
                vm.RefreshFromCurrentSession();
                var mon = session.Snapshot.Slots.First(s => s.Species > 0);
                vm.JumpTo(mon.Box, mon.Slot);
                var originalEvs = vm.EditEvs;
                var before = File.ReadAllBytes(path);
                var page = services.GetRequiredService<BoxBrowserPage>();
                await navigation.PushAsync(page);
                if (mode == "gifts")
                {
                    var host = (Grid)typeof(BoxBrowserPage).GetField("_hostGrid", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(page)!;
                    var task = EventGallery.ShowAsync(host, vm, session, null, () => { });
                    await Task.Delay(5000);
                    await SnapshotAsync(window, "gifts");
                    Log("Wonder card album opened");
                    router.Dispatch(PadButton.B);
                    await task;
                    Log("PASS");
                    return;
                }
                typeof(BoxBrowserPage).GetMethod("MaximizeIvs", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(page, null);
                if (vm.EditIvs != "31 31 31 31 31 31" || vm.EditEvs != originalEvs) throw new InvalidOperationException("Max IV changed the wrong spread");
                if (!File.ReadAllBytes(path).SequenceEqual(before)) throw new InvalidOperationException("Max wrote before Save");
                Log("Max IV sets all 31, preserves EV and leaves the file untouched until Save");
                // The real save command must survive an export/reopen of the copied save.
                await vm.SaveEditCommand.ExecuteAsync(null);
                Log($"Save result: {vm.Status}");
                await sessions.OpenAsync(new PickedDocument(path, "main"));
                vm.RefreshFromCurrentSession();
                vm.JumpTo(mon.Box, mon.Slot);
                if (vm.EditIvs != "31 31 31 31 31 31" || vm.EditEvs != originalEvs) throw new InvalidOperationException("Saved spread mismatch");
                Log("Max IV survived reopening the copied save from disk; EV unchanged");
                await Task.Delay(300);
                var editor = (ScrollView)typeof(BoxBrowserPage).GetField("_editorScroll", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(page)!;
                var targets = (Array)typeof(BoxBrowserPage).GetField("EditorFocusTargets", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(page)!;
                foreach (var target in targets)
                {
                    if ((string?)target!.GetType().GetProperty("Caption")!.GetValue(target) != "MAX IV") continue;
                    await editor.ScrollToAsync((View)target.GetType().GetProperty("View")!.GetValue(target)!, ScrollToPosition.Start, false);
                    break;
                }
                await Task.Delay(200);
                await SnapshotAsync(window, "stats");
            }
            else if (mode == "music")
            {
                var configured = IPlatformApplication.Current!.Services.GetRequiredService<IMusicPlayer>();
                Log($"Stored music library: {configured.Library.Count} tracks; menus source: {configured.SourceFor(MusicPlace.Menus).Kind}");
                using var player = new DesktopMusicPlayer(new MemoryPreferences());
                player.Play();
                if (player.IsOn || string.IsNullOrEmpty(player.LastError)) throw new InvalidOperationException("Empty library must explain how to add music");
                var path = Environment.GetEnvironmentVariable("PKFORGE_SMOKE_AUDIO")!;
                player.Add([new PickedDocument(path, Path.GetFileName(path))]);
                player.Play();
                await Task.Delay(3000);
                var native = (global::Windows.Media.Playback.MediaPlayer)typeof(DesktopMusicPlayer).GetField("_player", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(player)!;
                Log($"Music state={native.PlaybackSession.PlaybackState}; position={native.PlaybackSession.Position}; error={player.LastError}");
                if (!player.IsPlaying || native.PlaybackSession.Position.TotalSeconds < 0.5) throw new InvalidOperationException("Music did not start");
                player.Pause();
                await Task.Delay(250);
                if (player.IsPlaying || player.IsOn) throw new InvalidOperationException("Pause failed");
                player.Play();
                await Task.Delay(500);
                if (!player.IsPlaying) throw new InvalidOperationException("Resume failed");
                player.EnterPlace(MusicPlace.ParkDay);
                await Task.Delay(500);
                if (!player.IsPlaying) throw new InvalidOperationException("Music stopped on entering park");
                if (native.PlaybackSession.Position.TotalSeconds < 3) throw new InvalidOperationException("Inherited music restarted in the park");
                player.SetSource(MusicPlace.ParkDay, MusicSource.Silence);
                await Task.Delay(100);
                if (player.IsPlaying || !player.IsOn) throw new InvalidOperationException("Silence setting failed");
                player.EnterPlace(MusicPlace.Menus);
                await Task.Delay(500);
                if (!player.IsPlaying) throw new InvalidOperationException("Music did not resume on leaving a silent place");
                player.Add([new PickedDocument(path + ".missing.mp3", "Missing track")]);
                player.Skip();
                await Task.Delay(500);
                if (player.IsOn || string.IsNullOrEmpty(player.LastError)) throw new InvalidOperationException("Missing file was not reported");
                Log("Pause/resume, inherited playlist, silence, missing-file recovery passed");
            }
            Log("PASS");
        }
        catch (Exception error) { Log($"FAIL {error}"); }
        finally { Application.Current!.Quit(); }
    }

    private static async Task SnapshotAsync(Microsoft.Maui.Controls.Window window, string name)
    {
        var native = (Microsoft.UI.Xaml.Window)window.Handler!.PlatformView!;
        var bitmap = new RenderTargetBitmap();
        await bitmap.RenderAsync((Microsoft.UI.Xaml.UIElement)native.Content);
        var pixels = await bitmap.GetPixelsAsync();
        using var stream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96, 96, pixels.ToArray());
        await encoder.FlushAsync();
        stream.Seek(0);
        await using var file = File.Create(Path.Combine(Environment.GetEnvironmentVariable("PKFORGE_SMOKE_OUTPUT")!, name + ".png"));
        await stream.AsStreamForRead().CopyToAsync(file);
    }

    private sealed class MemoryPreferences : IPreferences
    {
        private readonly Dictionary<string, object> _values = [];
        public bool ContainsKey(string key, string? sharedName = null) => _values.ContainsKey(key);
        public void Remove(string key, string? sharedName = null) => _values.Remove(key);
        public void Clear(string? sharedName = null) => _values.Clear();
        public void Set<T>(string key, T value, string? sharedName = null) => _values[key] = value!;
        public T Get<T>(string key, T defaultValue, string? sharedName = null) => _values.TryGetValue(key, out var value) ? (T)value : defaultValue;
    }
}
#endif
