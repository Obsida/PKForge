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
            if (mode == "storage")
            {
                await CheckStorageAsync(window, navigation, router);
            }
            else if (mode == "events")
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

    private static async Task CheckStorageAsync(Microsoft.Maui.Controls.Window window, NavigationPage navigation, GamepadRouter router)
    {
        var services = IPlatformApplication.Current!.Services;
        var path = Path.Combine(Environment.GetEnvironmentVariable("PKFORGE_SMOKE_OUTPUT")!, "main");
        File.Copy(Environment.GetEnvironmentVariable("PKFORGE_SMOKE_SAVE")!, path, true);
        var sessions = services.GetRequiredService<ISaveSessionService>();
        Log("Opening copied save");
        await sessions.OpenAsync(new PickedDocument(path, "main"));
        Log("Copied save opened");
        var session = sessions.CurrentSession!;
        var data = services.GetRequiredService<IGameDataService>();
        var vm = services.GetRequiredService<BoxBrowserViewModel>();
        vm.RefreshFromCurrentSession();
        Log("Storage model refreshed");
        var page = services.GetRequiredService<BoxBrowserPage>();
        Log("Storage page constructed");
        await navigation.PushAsync(page);
        Log("Storage page pushed");
        await Task.Delay(500);
        var host = (Grid)typeof(BoxBrowserPage).GetField("_hostGrid", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(page)!;

        await CheckEditorInteractionAsync(window, navigation, router, page, host, vm, session);

        var features = (Task<GenerationRequest?>)typeof(GenerateWizard).GetMethod("ShowFeaturesFormAsync", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [host, data, session, new PickItem(350, "Milotic"), 0])!;
        Log("Creation form opened");
        window.MinimumWidth = 400;
        window.MinimumHeight = 400;
        foreach (var size in new[] { (1280, 800), (960, 640), (520, 640), (1280, 800) })
        {
            window.Width = size.Item1;
            window.Height = size.Item2;
            await Task.Delay(650);
            var overlay = (Grid)host.Children.Last();
            var panel = overlay.Children.OfType<Border>().Single();
            var create = Descendants(panel).OfType<Button>().Single(b => b.Text == "Создать");
            var cancel = Descendants(panel).OfType<Button>().Single(b => b.Text == "Отмена");
            foreach (var button in new[] { create, cancel })
            {
                var bounds = BoundsIn(button, host);
                if (bounds.Left < 0 || bounds.Top < 0 || bounds.Right > host.Width + 1 || bounds.Bottom > host.Height + 1 || bounds.Width < 40)
                    throw new InvalidOperationException($"Action button clipped at {size}: {bounds}, host={host.Bounds}");
            }
            var scroll = Descendants(panel).OfType<ScrollView>().Single();
            Log($"Measured form at {host.Width}x{host.Height}; scroll content={scroll.ContentSize}, viewport={scroll.Bounds}");
            var fields = Descendants(scroll).OfType<Label>().Where(l => l.Text is "Способность" or "Приём 4").ToArray();
            if (fields.Length != 2 || fields.Any(l => BoundsIn(l, host).Right > host.Width))
                throw new InvalidOperationException("Generation fields clipped horizontally");
            // WinUI may not signal ScrollFinished for a no-op scroll. Verify the
            // resulting offset instead of awaiting that event indefinitely.
            var bottom = Math.Max(0, scroll.ContentSize.Height - scroll.Height);
            _ = scroll.ScrollToAsync(0, bottom, false);
            await Task.Delay(100);
            if (scroll.ScrollY < bottom - 2) throw new InvalidOperationException("Cannot reach the end of the creation form");
            Log($"Creation form fits {host.Width}x{host.Height}; actions visible, final move reachable; scroll={scroll.ScrollY}");
            await SnapshotAsync(window, $"create-{size.Item1}");
        }
        router.Dispatch(PadButton.B);
        if (await features is not null) throw new InvalidOperationException("Cancel generated a Pokémon");

        var abilityChoices = InfoPickers.AbilityItems(data, session, 350, 0);
        if (!abilityChoices.Any(a => a.Id == 63 && a.Name == "Чудесная чешуя" && a.Detail!.Contains("Защиту")))
            throw new InvalidOperationException("Milotic ability name/description not translated");
        Log($"Russian ability names/effects: {RussianAbilities.Entries.Count} entries; Milotic verified");

        // Exercise the actual add picker after changing the bag, then again after removing the item.
        var pouch = session.GetBag().First();
        var item = BagItemChoices.AvailableToAdd(session.GetBag(), session.GetPouchLegalItems(pouch.Name), session.GetItemNames()).First();
        session.SetItemCount(pouch.Name, item, 1);
        var bagType = typeof(BoxBrowserPage).GetNestedType("BagEditor", BindingFlags.NonPublic)!;
        var bagTask = (Task)bagType.GetMethod("ShowAsync", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [host, session, vm, data])!;
        var bag = PadHandlers(router).Single(h => h.GetType() == bagType);
        async Task CheckItemPicker(bool shouldContain)
        {
            var add = (Task)bagType.GetMethod("AddItemAsync", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(bag, null)!;
            await Task.Delay(300);
            var picker = PadHandlers(router).OfType<PickerMenu>().Single();
            var choices = (IReadOnlyList<PickItem>)typeof(PickerMenu).GetField("_all", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(picker)!;
            if (choices.Any(c => c.Id == item) != shouldContain) throw new InvalidOperationException("Owned item filtering is stale");
            router.Dispatch(PadButton.B);
            await add;
        }
        await CheckItemPicker(false);
        session.SetItemCount(pouch.Name, item, 0);
        await CheckItemPicker(true);
        router.Dispatch(PadButton.B);
        await bagTask;
        Log("Real add picker hides an owned item and restores it after removal");

        // Put an existing mon in a box on the copied save, so the party is never emptied.
        var source = session.Snapshot.Slots.First(s => s.Species > 0);
        var target = session.Snapshot.Slots.First(s => s.Box >= 0 && s.Species is null);
        var exported = session.ExportSlot(source.Box, source.Slot);
        if (!await vm.RunMutationAsync(s => new GenerationOutcome(s.ImportSlot(target.Box, target.Slot, exported.Data, exported.Format), "Diagnostic copy"), source.Slot, refreshSlot: false))
            throw new InvalidOperationException(vm.Status);
        vm.RefreshFromCurrentSession();
        vm.JumpTo(target.Box, target.Slot);
        var before = File.ReadAllBytes(path);
        var backupService = services.GetRequiredService<IBackupService>();
        var backups = (await backupService.ListAsync()).Count;
        var deleteMethod = typeof(BoxBrowserPage).GetMethod("ReleaseSlotAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var nickname = session.ReadEntity(target.Box, target.Slot).Nickname;
        var deletion = (Task)deleteMethod.Invoke(page, [target.Slot, nickname, false])!;
        if (!File.ReadAllBytes(path).SequenceEqual(before)) throw new InvalidOperationException("Delete wrote before confirmation");
        router.Dispatch(PadButton.B);
        await deletion;
        if (session.ReadEntity(target.Box, target.Slot).IsEmpty || !File.ReadAllBytes(path).SequenceEqual(before))
            throw new InvalidOperationException("Cancel deleted the Pokémon");
        deletion = (Task)deleteMethod.Invoke(page, [target.Slot, nickname, false])!;
        await Task.Delay(300);
        await SnapshotAsync(window, "delete-confirmation");
        router.Dispatch(PadButton.A);
        await deletion;
        if (!session.ReadEntity(target.Box, target.Slot).IsEmpty || (await backupService.ListAsync()).Count != backups + 1)
            throw new InvalidOperationException($"Delete/restore point failed: {vm.Status}");
        await sessions.OpenAsync(new PickedDocument(path, "main"));
        if (!sessions.CurrentSession!.ReadEntity(target.Box, target.Slot).IsEmpty)
            throw new InvalidOperationException("Deleted Pokémon returned after reopening");
        Log("Delete cancellation preserves bytes; confirmed deletion survives reopen and creates a restore point");
    }

    private static IEnumerable<IPadHandler> PadHandlers(GamepadRouter router) =>
        (IEnumerable<IPadHandler>)typeof(GamepadRouter).GetField("_stack", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(router)!;

    private static async Task UntilAsync(Func<bool> ready)
    {
        var deadline = Environment.TickCount64 + 10000;
        while (!ready())
        {
            if (Environment.TickCount64 > deadline) throw new TimeoutException("UI step did not become ready");
            await Task.Delay(25);
        }
    }

    private static async Task CheckEditorInteractionAsync(Microsoft.Maui.Controls.Window window, NavigationPage navigation,
        GamepadRouter router, BoxBrowserPage page, Grid host, BoxBrowserViewModel vm, ISaveEngineSession session)
    {
        var mon = session.Snapshot.Slots.First(s => s.Species > 0);
        vm.JumpTo(mon.Box, mon.Slot);
        vm.EditFriendship = ((session.ReadEntity(mon.Box, mon.Slot).Friendship + 1) % 256).ToString();
        var saveButton = Descendants(host).OfType<Button>().Single(b => b.Text == "Сохранить изменения");
        saveButton.SendClicked();
        var saving = vm.SaveEditCommand.ExecutionTask ?? throw new InvalidOperationException("Save button did not execute its command");
        if (!vm.IsSavingEdit || vm.EditSaveStatus.Length == 0 || saveButton.Text != "Сохранение…")
            throw new InvalidOperationException("Save has no immediate progress feedback");
        await saving;
        if (vm.IsSavingEdit || !vm.EditSaveStatus.StartsWith("Сохранено.") || saveButton.Text != "Сохранить изменения")
            throw new InvalidOperationException($"Save feedback did not finish: {vm.EditSaveStatus}");
        if (!Descendants(host).OfType<Label>().Any(l => l.Text == vm.EditSaveStatus))
            throw new InvalidOperationException("Save result is not visible beside the button");
        await vm.SaveEditCommand.ExecuteAsync(null);
        if (!vm.EditSaveStatus.StartsWith("Изменений нет.")) throw new InvalidOperationException("No-op save lacks feedback");
        var editor = (ScrollView)typeof(BoxBrowserPage).GetField("_editorScroll", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(page)!;
        await Task.Delay(250);
        var feedback = Descendants(host).OfType<Label>().Single(l => l.Text == vm.EditSaveStatus);
        if (BoundsIn(feedback, editor).Bottom - editor.ScrollY > editor.Height + 1)
            throw new InvalidOperationException("Save result scrolled below the visible editor");
        await SnapshotAsync(window, "save-feedback");
        router.Dispatch(PadButton.B);
        if (navigation.CurrentPage != page || vm.SelectedSlot >= 0)
            throw new InvalidOperationException("Esc left storage instead of returning from the selected Pokémon");
        Log("Save button reports progress, success and no-op; Esc returns from selection to the box");

        var target = session.Snapshot.Slots.First(s => s.Box >= 0 && s.Species is null);
        vm.JumpTo(target.Box, target.Slot);
        var add = (Task)typeof(BoxBrowserPage).GetMethod("OfferAddPokemonAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(page, [target.Slot])!;
        router.Dispatch(PadButton.A);
        await UntilAsync(() => PadHandlers(router).LastOrDefault() is PokedexPicker);
        var picker = PadHandlers(router).OfType<PokedexPicker>().Single();
        var grid = (DexGridView)typeof(PokedexPicker).GetField("_grid", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(picker)!;
        await UntilAsync(() => grid.Count > 100 && grid.CanvasSize.Height > 0);
        await Task.Delay(250);
        var scroller = (TouchScroller)typeof(DexGridView).GetField("_scroller", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(grid)!;
        var touch = typeof(DexGridView).GetMethod("OnTouch", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)!;
        void Touch(SKTouchAction action, SKPoint point, int wheel = 0) => touch.Invoke(grid,
            [grid, new SKTouchEventArgs(1, action, SKMouseButton.Left, SKTouchDeviceType.Mouse, point, action != SKTouchAction.Released, wheel)]);
        Touch(SKTouchAction.WheelChanged, new SKPoint(100, 100), -360);
        var thumb = scroller.Thumb();
        var down = new SKPoint(thumb.MidX, thumb.MidY);
        var bottom = new SKPoint(thumb.MidX, grid.CanvasSize.Height * 0.8f);
        Touch(SKTouchAction.Pressed, down);
        Touch(SKTouchAction.Moved, bottom);
        Touch(SKTouchAction.Released, bottom);
        var offsetField = typeof(DexGridView).GetField("_scroll", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var offset = (float)offsetField.GetValue(grid)!;
        if (offset < 100) throw new InvalidOperationException("Dragging the scrollbar did not scroll");
        // Hover events are the real Windows sequence after releasing the scrollbar.
        Touch(SKTouchAction.Moved, new SKPoint(thumb.MidX, bottom.Y + 20));
        Touch(SKTouchAction.Moved, new SKPoint(100, bottom.Y - 40));
        Touch(SKTouchAction.Released, new SKPoint(100, bottom.Y - 40));
        await Task.Delay(350);
        if (Math.Abs((float)offsetField.GetValue(grid)! - offset) > 1)
            throw new InvalidOperationException("Scrollbar release/hover reset the list");
        await SnapshotAsync(window, "dex-scrolled");
        Log($"Dex scrollbar remains at {offset:0} after release and hover; mouse wheel works");

        grid.SetCursor(0); // Bulbasaur: no form submenu, so Back returns straight to species.
        router.Dispatch(PadButton.A);
        await UntilAsync(() => PadHandlers(router).LastOrDefault() is PadOverlay);
        router.Dispatch(PadButton.B);
        await UntilAsync(() => PadHandlers(router).LastOrDefault() is PokedexPicker);
        router.Dispatch(PadButton.B);
        await UntilAsync(() => PadHandlers(router).LastOrDefault() is PadMenu);
        router.Dispatch(PadButton.B);
        await add;
        if (navigation.CurrentPage != page) throw new InvalidOperationException("Wizard Esc exited to the main menu");
        Log("Esc walks features → species → add menu → box, preserving the storage page");
    }

    private static IEnumerable<VisualElement> Descendants(IVisualTreeElement element)
    {
        foreach (var child in element.GetVisualChildren())
        {
            if (child is VisualElement visual) yield return visual;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }

    private static Rect BoundsIn(VisualElement element, VisualElement host)
    {
        double x = 0, y = 0;
        for (Element? parent = element; parent is VisualElement visual && parent != host; parent = parent.Parent)
        {
            x += visual.X;
            y += visual.Y;
        }
        return new Rect(x, y, element.Width, element.Height);
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
