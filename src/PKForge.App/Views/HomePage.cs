using Microsoft.Maui.Controls.Shapes;
using PKForge.App.Services;
using PKForge.App.Theme;
using PKForge.App.ViewModels;
using PKForge.Domain;

namespace PKForge.App.Views;

/// <summary>
/// The console home: a horizontal shelf of game cartridges, a first-run wizard when
/// nothing is linked, and a bottom hint bar. Landscape composition for the AYN Thor.
/// </summary>
public sealed class HomePage : ContentPage, IPadHandler
{
    private readonly SavePickerViewModel _viewModel;
    private ScrollView _shelf = null!;
    private HorizontalStackLayout _shelfItems = null!;
    private int _shelfIndex = -1;
    private DsCard[] _cards = [];
    private int _zone;       // 0 = game shelf, 1 = the destination cards
    private int _cardIndex;
    private PokeparkPage? _parkPage;
    private int _parkNavigationPending;

    public HomePage(SavePickerViewModel viewModel)
    {
        _viewModel = viewModel;
        BindingContext = viewModel;
        Title = "PKForge";
        BackgroundColor = UiTokens.Housing;
        NavigationPage.SetHasNavigationBar(this, false);

        // GAMES section: the cartridge shelf (reused), labelled DS-style.
        var gamesLabel = new Label
        {
            Text = "Игры", FontFamily = DsChrome.PixelFont, FontSize = UiTokens.TextTitle,
            TextColor = UiTokens.Ink0, VerticalOptions = LayoutOptions.Center,
        };
        var filterCaption = new Label
        {
            FontFamily = DsChrome.PixelFont, FontSize = UiTokens.TextLabel,
            TextColor = UiTokens.Ink0, VerticalOptions = LayoutOptions.Center,
        };
        filterCaption.SetBinding(Label.TextProperty, new Binding("Caption",
            source: _viewModel.Filter, stringFormat: "Фильтр · {0}"));
        // A menu button (the summary's resting tab), not an outlined pill.
        var filterChip = Kit.Tab("Фильтр");
        filterChip.Content = filterCaption;
        Kit.SetTab(filterChip, false);
        filterCaption.TextColor = UiTokens.Ink0;
        filterChip.Padding = new Thickness(12, 4);
        filterChip.HorizontalOptions = LayoutOptions.End;
        var filterTap = new TapGestureRecognizer();
        filterTap.Tapped += (_, _) => _ = ShowFilterMenuAsync();
        filterChip.GestureRecognizers.Add(filterTap);
        var headerRow = new Grid
        {
            ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto)],
            Children = { gamesLabel, filterChip },
        };
        _shelfItems = new HorizontalStackLayout
        {
            Spacing = 12,
            VerticalOptions = LayoutOptions.Center,
        };
        BindableLayout.SetItemsSource(_shelfItems, _viewModel.Groups);
        BindableLayout.SetItemTemplate(_shelfItems, new DataTemplate(BuildCartridgeTile));
        _shelf = new ScrollView
        {
            Orientation = ScrollOrientation.Horizontal,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Never,
            VerticalScrollBarVisibility = ScrollBarVisibility.Never,
            VerticalOptions = LayoutOptions.Center,
            Content = _shelfItems,
        };
        var shelfArea = new Grid
        {
            RowSpacing = 6,
            RowDefinitions = [new(GridLength.Auto), new(GridLength.Star)],
            Children = { headerRow, _shelf },
        };
        Grid.SetRow(_shelf, 1);
        BlockNativeFocus(_shelf);

        // The three destinations as PKSM tiles with bundled pixel icons.
        var bank = new DsCard("bank", "Банк") { Tapped = () => _ = PushAsync<BankPage>() };
        var autopilot = new DsCard("pokedex", "Автопилот покедекса") { Tapped = () => _ = OpenAutopilotAsync() };
        var park = new DsCard("park", "Покепарк") { Tapped = () => _ = PushParkAsync() };
        var events = new DsCard("events", "События") { Tapped = () => _ = ShowEventsMenuAsync() };
        var settings = new DsCard("settings", "Настройки") { Tapped = () => _ = ShowSettingsAsync() };
        _cards = [bank, autopilot, park, events, settings];
        foreach (var card in _cards) BlockNativeFocus(card);
        var cards = new Grid
        {
            ColumnSpacing = 10,
            ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star), new(GridLength.Star), new(GridLength.Star), new(GridLength.Star)],
            Children = { bank, autopilot, park, events, settings },
        };
        Grid.SetColumn(autopilot, 1);
        Grid.SetColumn(park, 2);
        Grid.SetColumn(events, 3);
        Grid.SetColumn(settings, 4);

        var body = new Grid
        {
            Padding = new Thickness(14, 10),
            RowSpacing = 10,
            RowDefinitions = [new(GridLength.Star), new(GridLength.Auto)],
            Children = { shelfArea, cards },
        };
        Grid.SetRow(cards, 1);
        var bodyHost = new Grid { Children = { DsChrome.GridBackground(), body } };

        var footer = DsChrome.Footer(
            ("A", "Открыть", () => OnPadButton(PadButton.A)),
            // The cartridge identity menu (rename, color, which game): B on the pad, a hold by touch.
            ("B", "Настроить игру · правый щелчок", () =>
            {
                if ((uint)_shelfIndex < (uint)_viewModel.Groups.Count) _ = ShowSaveMenuAsync(_viewModel.Groups[_shelfIndex]);
            }),
            ("L", "Фильтр", () => _ = ShowFilterMenuAsync()),
            ("Y", "Подключить", () => _ = ShowLinkMenuAsync()),
            ("X", "Файл", () => _ = LinkFileAsync()),
            ("+", "Настройки", () => _ = ShowSettingsAsync()));

        var root = new Grid
        {
            RowDefinitions = [new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto)],
            Children = { DsChrome.TitleBar(), DsChrome.StatusStrip("PKForge", "Без сети"), bodyHost, footer },
        };
        Grid.SetRow((View)root.Children[1], 1);
        Grid.SetRow(bodyHost, 2);
        Grid.SetRow(footer, 3);

        _hostGrid = new Grid { Children = { root } };
        Content = _hostGrid;
    }

    private Grid _hostGrid = null!;

    /// <summary>
    /// The shelf's selection is drawn by PKForge (the cyan frame); Android's own focused
    /// state is the grey box that sticks around until the next tap, so it is disabled here.
    /// </summary>
    private static void BlockNativeFocus(View view)
    {
#if ANDROID
        view.HandlerChanged += (_, _) =>
        {
            if (view.Handler?.PlatformView is Android.Views.View platform)
            {
                platform.Focusable = false;
                platform.FocusableInTouchMode = false;
            }
        };
#endif
    }

    /// <summary>
    /// Handles touch on <paramref name="view"/> from the native touch stream: a press released
    /// within the touch slop is a tap, one that rests for the platform long-press timeout is a
    /// long press. Both live here because subscribing to the native stream replaces the
    /// listener a MAUI tap recognizer relies on, so a recognizer on the same view never fires.
    /// A drag that scrolls the shelf cancels the press.
    /// </summary>
    private static void AttachPress(View view, Action onTap, Action onLongPress)
    {
#if ANDROID
        view.HandlerChanged += (_, _) =>
        {
            if (view.Handler?.PlatformView is not Android.Views.View platform) return;
            var slop = Android.Views.ViewConfiguration.Get(platform.Context!)!.ScaledTouchSlop;
            var timeout = Android.Views.ViewConfiguration.LongPressTimeout;
            var token = 0;
            var pressing = false;
            float downX = 0, downY = 0;
            platform.Touch += (_, e) =>
            {
                var motion = e.Event!;
                switch (motion.ActionMasked)
                {
                    case Android.Views.MotionEventActions.Down:
                        var pressed = ++token;
                        pressing = true;
                        downX = motion.GetX();
                        downY = motion.GetY();
                        view.Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(timeout), () =>
                        {
                            if (pressed != token || !pressing) return;
                            pressing = false;
                            platform.PerformHapticFeedback(Android.Views.FeedbackConstants.LongPress);
                            onLongPress();
                        });
                        break;
                    case Android.Views.MotionEventActions.Move:
                        if (Math.Abs(motion.GetX() - downX) > slop || Math.Abs(motion.GetY() - downY) > slop)
                            pressing = false;
                        break;
                    case Android.Views.MotionEventActions.Up:
                        token++;
                        if (pressing) onTap();
                        pressing = false;
                        break;
                    case Android.Views.MotionEventActions.Cancel:
                        token++;
                        pressing = false;
                        break;
                }
                e.Handled = true;
            };
        };
#elif WINDOWS
        view.HandlerChanged += (_, _) =>
        {
            if (view.Handler?.PlatformView is not Microsoft.UI.Xaml.UIElement platform) return;
            platform.Tapped += (_, e) => { onTap(); e.Handled = true; };
            platform.RightTapped += (_, e) => { onLongPress(); e.Handled = true; };
        };
#endif
    }

    // Once per launch, not per page: a color scheme change rebuilds Home, and the new page
    // must not scan, welcome or check for updates again.
    private static bool _welcomeShown;
    private static bool _scannedOnce;
    private static bool _updateCheckQueued;
    private bool _isAppearing;
    private bool _eventsSubscribed;
    private AvailableAppUpdate? _pendingAuthorizedUpdate;

    /// <summary>The Thor's lower screen is on from launch - the app *is* dual-screen.</summary>
    private SecondScreenClaim? _secondClaim;

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _isAppearing = true;
        if (!_crashOffered)
        {
            _crashOffered = true;
            _ = OfferCrashReportAsync();
        }
        _ = IPlatformApplication.Current?.Services.GetService<SpritePackDownloader>()?.FetchAddOnsQuietlyAsync();
        // The lower screen shows the shelf's highlighted game while Home is in front.
        _secondClaim ??= IPlatformApplication.Current?.Services.GetService<SecondScreenState>()?.Routes.CreateClaim(SecondScreenOwner.Home);
        _secondClaim?.Activate();
        if (!_eventsSubscribed)
        {
            App.Resumed += OnAppResumed;
            _viewModel.LinkReported += OnLinkReported;
            _eventsSubscribed = true;
        }
        _zone = 0;
        ClearCardFocus();
        IPlatformApplication.Current?.Services.GetService<GamepadRouter>()?.Push(this);
        // Scan once per launch; coming back from the box must not trigger a re-walk.
        if (!_scannedOnce)
        {
            _scannedOnce = true;
            var park = IPlatformApplication.Current?.Services.GetService<PokeparkService>();
            if (park is not null)
            {
                _ = Task.Run(() =>
                {
                    park.EnsureInitialized();
                    HabitatCatalog.Warm(park.LoadRoster().Select(mon => (mon.Species, mon.Form)));
                });
            }
            _viewModel.RescanCommand.Execute(null);
        }
        var host = IPlatformApplication.Current?.Services.GetService<ISecondaryDisplayHost>();
        if (host?.IsAvailable == true)
        {
            try { _ = host.ShowAsync(); }
            catch { }
        }
        if (_parkPage is null)
        {
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(250), () =>
            {
                if (_parkPage is null)
                    _parkPage = IPlatformApplication.Current?.Services.GetService<PokeparkPage>();
            });
        }

        // Returning from Android's install-unknown-apps screen finishes an update the
        // user already accepted with YES. No second question, no manual Check for update.
        if (_pendingAuthorizedUpdate is { } authorized && AppUpdateService.CanInstallUpdates())
        {
            _pendingAuthorizedUpdate = null;
            Dispatcher.DispatchAsync(async () => await InstallUpdateAsync(authorized));
            return;
        }

        // First run: welcome as the same in-world menu everything else uses, no special panel.
        if (_viewModel.ShowWizard && !_welcomeShown)
        {
            _welcomeShown = true;
            Dispatcher.Dispatch(async () =>
            {
                // First run plays like a Pokémon intro: a professor-style dialogue, then the choices.
                await DialogueBox.ShowSequenceAsync(_hostGrid,
                    "Привет! Добро пожаловать в PKForge!",
                    "Здесь покемоны из разных игр могут безопасно жить вместе.",
                    "Можно редактировать покемонов, хранить их в банке и проверять легальность.",
                    "Перед каждой записью создаётся резервная копия для восстановления.",
                    "А теперь… найдём твои игры!");

                var choice = await PadMenu.ShowAsync(_hostGrid, "Начать", null,
                    new PadOption("Подключить эмулятор", IconPath: "link", Detail: "Выбрать папку эмулятора с сохранениями игр."),
                    new PadOption("Открыть отдельное сохранение", IconPath: "file", Detail: "Выбрать отдельный файл сохранения."),
                    new PadOption($"Скачать набор спрайтов ({SpritePackDownloader.SizeHint})", IconPath: "download", Detail: "Анимации, изображения HOME и значки предметов для работы без сети."),
                    new PadOption("Позже", IconPath: "close"));
                switch (choice)
                {
                    case "Подключить эмулятор": await ShowLinkMenuAsync(); break;
                    case "Открыть отдельное сохранение": await LinkFileAsync(); break;
                    case var pack when pack?.StartsWith("Скачать набор спрайтов", StringComparison.Ordinal) == true:
                        await DownloadSpritePackAsync();
                        break;
                }
                _viewModel.CompleteSetup();
                await CheckForUpdateAsync(automatic: true);
            });
        }
        else if (!_updateCheckQueued)
        {
            _updateCheckQueued = true;
            Dispatcher.Dispatch(async () => await CheckForUpdateAsync(automatic: true));
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _isAppearing = false;
        _secondClaim?.Release();
        if (_eventsSubscribed)
        {
            App.Resumed -= OnAppResumed;
            _viewModel.LinkReported -= OnLinkReported;
            _eventsSubscribed = false;
        }
        IPlatformApplication.Current?.Services.GetService<GamepadRouter>()?.Remove(this);
    }

    private void OnLinkReported(string title, string message) =>
        MainThread.BeginInvokeOnMainThread(() => _ = PadMenu.ShowAsync(_hostGrid, title, message, "OK"));

    /// <summary>Android settings do not trigger MAUI page appearing again; resume does.</summary>
    private void OnAppResumed()
    {
        if (_pendingAuthorizedUpdate is not { } authorized || !AppUpdateService.CanInstallUpdates())
            return;

        _pendingAuthorizedUpdate = null;
        Dispatcher.DispatchAsync(async () => await InstallUpdateAsync(authorized));
    }

    /// <summary>Every button on the home screen, in one place.</summary>
    public bool OnPadButton(PadButton button)
    {
        switch (button)
        {
            case PadButton.Down when _zone == 0: _zone = 1; return FocusCard(0);
            case PadButton.Up when _zone == 1: _zone = 0; ClearCardFocus(); return true;
            case PadButton.Left: return _zone == 0 ? MoveShelf(-1) : FocusCard(_cardIndex - 1);
            case PadButton.Right: return _zone == 0 ? MoveShelf(1) : FocusCard(_cardIndex + 1);
            case PadButton.A:
                if (_zone == 1) { _cards[_cardIndex].Tapped?.Invoke(); return true; }
                return OpenShelfSelection();
            case PadButton.L: _ = ShowFilterMenuAsync(); return true;
            // B on a highlighted cartridge: its identity menu (rename, color, set game).
            case PadButton.B when _zone == 0 && _shelfIndex >= 0 && _shelfIndex < _viewModel.Groups.Count:
                _ = ShowSaveMenuAsync(_viewModel.Groups[_shelfIndex]);
                return true;
            case PadButton.X: _ = LinkFileAsync(); return true;
            case PadButton.Y: _ = ShowLinkMenuAsync(); return true;
            case PadButton.R: _ = PushAsync<BackupHistoryPage>(); return true;
            case PadButton.Start: _ = ShowSettingsAsync(); return true;
            default: return false;
        }
    }

    /// <summary>Move the cursor among the Bank/Events/Settings cards (the second focus zone).</summary>
    private bool FocusCard(int index)
    {
        if (_cards.Length == 0) return false;
        _cardIndex = Math.Clamp(index, 0, _cards.Length - 1);
        for (var i = 0; i < _cards.Length; i++) _cards[i].Selected = i == _cardIndex;
        return true;
    }

    private void ClearCardFocus()
    {
        foreach (var card in _cards) card.Selected = false;
    }

    private bool MoveShelf(int delta)
    {
        if (_viewModel.Groups.Count == 0) return false;
        _shelfIndex = Math.Clamp(_shelfIndex < 0 ? 0 : _shelfIndex + delta, 0, _viewModel.Groups.Count - 1);
        var selected = _viewModel.Groups[_shelfIndex];
        for (var i = 0; i < _viewModel.Groups.Count; i++)
            _viewModel.Groups[i].IsSelected = i == _shelfIndex;
        if (_shelfIndex < _shelfItems.Children.Count && _shelfItems.Children[_shelfIndex] is View selectedView)
        {
            var centeredX = Math.Max(0, selectedView.X - Math.Max(0, (_shelf.Width - selectedView.Width) / 2));
            // Smooth scroll for single steps; snap while the pad repeats fast. Overlapping
            // animated scrolls during held navigation made the shelf teleport.
            var snap = Environment.TickCount64 - _lastShelfMoveMs < 200;
            _ = _shelf.ScrollToAsync(centeredX, 0, !snap);
        }
        _lastShelfMoveMs = Environment.TickCount64;
        // The lower screen previews the highlighted game's hero art.
        var state = IPlatformApplication.Current?.Services.GetService<SecondScreenState>();
        if (state is not null) state.PreviewGame = selected.Save;
        return true;
    }

    private long _lastShelfMoveMs;

    private bool OpenShelfSelection()
    {
        if (_shelfIndex < 0 || _shelfIndex >= _viewModel.Groups.Count) return MoveShelf(0);
        _ = OpenCardAsync(_viewModel.Groups[_shelfIndex]);
        return true;
    }

    /// <summary>Narrows the cartridge shelf: every game, one generation, one console,
    /// or plain alphabetical.</summary>
    private async Task ShowFilterMenuAsync()
    {
        var choice = await PadMenu.ShowAsync(_hostGrid, "Фильтр игр", null,
            new PadOption("Все игры", IconPath: "all"),
            new PadOption("Порядок выхода", IconPath: "calendar", Detail: "Все игры, от старых до новых."),
            new PadOption("По алфавиту (A–Z)", IconPath: "alpha"),
            new PadOption("Game Boy (Gen I-II)", IconPath: "platform-gb"),
            new PadOption("GBA (Gen III)", IconPath: "platform-gba"),
            new PadOption("DS (Gen IV-V)", IconPath: "platform-ds"),
            new PadOption("3DS (Gen VI-VII)", IconPath: "azahar"),
            new PadOption("Switch (Gen VII-IX)", IconPath: "eden"),
            new PadOption("Поколение I", IconPath: "generation"),
            new PadOption("Поколение II", IconPath: "generation"),
            new PadOption("Поколение III", IconPath: "generation"),
            new PadOption("Поколение IV", IconPath: "generation"),
            new PadOption("Поколение V", IconPath: "generation"),
            new PadOption("Поколение VI", IconPath: "generation"),
            new PadOption("Поколение VII", IconPath: "generation"),
            new PadOption("Поколение VIII", IconPath: "generation"),
            new PadOption("Поколение IX", IconPath: "generation"));
        var (key, caption) = choice switch
        {
            "Все игры" => ("all", "Все"),
            "Порядок выхода" => ("release", "По выходу"),
            "По алфавиту (A–Z)" => ("az", "A-Z"),
            "Game Boy (Gen I-II)" => ("gb", "Game Boy"),
            "GBA (Gen III)" => ("gba", "GBA"),
            "DS (Gen IV-V)" => ("ds", "DS"),
            "3DS (Gen VI-VII)" => ("3ds", "3DS"),
            "Switch (Gen VII-IX)" => ("switch", "Switch"),
            "Поколение I" => ("gen1", "Поколение I"),
            "Поколение II" => ("gen2", "Поколение II"),
            "Поколение III" => ("gen3", "Поколение III"),
            "Поколение IV" => ("gen4", "Поколение IV"),
            "Поколение V" => ("gen5", "Поколение V"),
            "Поколение VI" => ("gen6", "Поколение VI"),
            "Поколение VII" => ("gen7", "Поколение VII"),
            "Поколение VIII" => ("gen8", "Поколение VIII"),
            "Поколение IX" => ("gen9", "Поколение IX"),
            _ => ("all", "Все"),
        };
        _viewModel.ApplyFilter(key, caption);
        // Groups were replaced wholesale: re-run selection so a cartridge is highlighted.
        MoveShelf(_shelfIndex);
    }

    /// <summary>A cartridge with one save opens it; a shared cartridge asks which save first.</summary>
    private async Task OpenCardAsync(SaveCard card)
    {
        var save = await ChooseSaveAsync(card, "Какое сохранение открыть?");
        if (save is not null) await OpenSaveAsync(save);
    }

    /// <summary>Long-press / B: the save's identity menu ("Open save" first); a shared cartridge picks the save first.</summary>
    private async Task ShowSaveMenuAsync(SaveCard card)
    {
        var save = await ChooseSaveAsync(card, "Выбери сохранение для изменения имени, цвета или версии игры.");
        if (save is null) return;
        if (await SaveIdentitySheet.ShowAsync(_hostGrid, _viewModel, save))
            await OpenSaveAsync(save);
    }

    private Task<DetectedSave?> ChooseSaveAsync(SaveCard card, string message) =>
        SavePickerSheet.ChooseFromTileAsync(_hostGrid, card.Saves,
            $"{card.DisplayName} · {card.SaveCount} saves", message);

    /// <summary>
    /// First open of a save whose editions share one format (FireRed/LeafGreen, Ruby/Sapphire,
    /// Diamond/Pearl): the bytes cannot say which cartridge it is, so the player picks once and
    /// the choice sticks (it stays editable from the save's menu). False when they backed out.
    /// </summary>
    private async Task<bool> ChooseEditionOnceAsync(DetectedSave save)
    {
        var store = _viewModel.Identities;
        var current = store.Get(save.DocumentId) ?? new SaveIdentity(save.DocumentId);
        if (_editionDeferred.Contains(save.DocumentId) || save.Guess is not { } guess
            || !SaveIdentityRules.NeedsEditionChoice(guess, current.GameChoiceId))
            return true;

        const string later = "Решить позже";
        var editions = SaveIdentityRules.ChoicesFor(guess.Family, guess.Label).Where(c => !c.IsHack).ToArray();
        var options = editions.Select(c => new PadOption(c.Label, IconPath: "game"))
            .Append(new PadOption(later, IconPath: "close")).ToArray();
        var choice = await PadMenu.ShowAsync(_hostGrid, "Какая это версия игры?",
            "Обе версии используют одинаковый формат сохранения, поэтому PKForge не может различить их. " +
            "Выбери версию. Изменить её можно в меню сохранения (правый щелчок на ПК).", options);
        if (choice is null) return false;
        if (choice == later)
        {
            _editionDeferred.Add(save.DocumentId);
            return true;
        }
        store.Set(current with { GameChoiceId = editions.First(c => c.Label == choice).Id });
        return true;
    }

    /// <summary>Saves whose edition the player chose to decide later: not asked again this session.</summary>
    private readonly HashSet<string> _editionDeferred = new(StringComparer.Ordinal);

    /// <summary>Platform folders keep emulator choices together for touch and controller use.</summary>
    private async Task ShowLinkMenuAsync()
    {
        while (true)
        {
            var platform = await PadMenu.ShowAsync(_hostGrid, "Подключить хранилище", "Выбери платформу, чтобы увидеть её эмуляторы.",
                new PadOption("Game Boy / Game Boy Color", IconPath: "platform-gb"),
                new PadOption("Game Boy Advance", IconPath: "platform-gba"),
                new PadOption("Nintendo DS", IconPath: "platform-ds"),
                new PadOption("GameCube", IconPath: "platform-gc"),
                new PadOption("Nintendo 3DS", IconPath: "azahar"),
                new PadOption("Nintendo Switch", IconPath: "eden"),
                new PadOption("Отдельное сохранение", IconPath: "file"));
            if (platform is null) return;
            if (platform == "Отдельное сохранение") { await LinkFileAsync(); return; }
            var options = platform switch
            {
                "Game Boy / Game Boy Color" => new[]
                {
                    new PadOption("RetroArch", IconPath: "retroarch"),
                    new PadOption("Linkboy", IconPath: "linkboy"),
                    new PadOption("Pizza Boy C (GB/GBC)", IconPath: "pizzaboy"),
                },
                "Game Boy Advance" => new[]
                {
                    new PadOption("RetroArch", IconPath: "retroarch"),
                    new PadOption("Linkboy", IconPath: "linkboy"),
                    new PadOption("Pizza Boy A (GBA)", IconPath: "pizzaboy"),
                },
                "Nintendo DS" => new[]
                {
                    new PadOption("melonDS", IconPath: "melonds"),
                    new PadOption("DraStic", IconPath: "drastic"),
                    new PadOption("RetroArch", IconPath: "retroarch"),
                },
                "GameCube" => new[] { new PadOption("Dolphin", IconPath: "dolphin") },
                "Nintendo 3DS" => new[]
                {
                    new PadOption("Azahar / Lime3DS", IconPath: "azahar"),
                    new PadOption("Citra MMJ", IconPath: "azahar"),
                },
                _ => new[] { new PadOption("Eden", IconPath: "eden") },
            };
            var choice = await PadMenu.ShowAsync(_hostGrid, platform, null, options);
            if (choice is null) continue;
            var guidance = choice switch
            {
                "Dolphin" => "Save in game and stop emulation before editing Colosseum or XD. Select Dolphin's GC folder, a region folder, or Card A / Card B containing .gci saves. For .raw memory cards, export the game as GCI with Dolphin's Memory Card Manager, or configure that card slot as GCI Folder. After editing, start the game normally; loading an old save state can undo your edits.",
                "DraStic" => "Save in game and close DraStic. Select its backup folder containing .dsv battery saves, or the DraStic data folder. Save states are not supported. Restart the game normally after editing.",
                "Pizza Boy A (GBA)" or "Pizza Boy C (GB/GBC)" => "Save in game and close Pizza Boy. Select the folder containing its battery saves (.sav), not save states. If Android hides the folder, export the battery save in Pizza Boy and link that export. Import the edited export back into Pizza Boy, then restart the game normally.",
                "Azahar / Lime3DS" => "Сохранись в игре и закрой эмулятор. Выбери пользовательскую папку с sdmc, саму sdmc или Nintendo 3DS. После редактирования запусти игру обычным способом.",
                "Citra MMJ" => "Save in game and close Citra MMJ. In the file picker open the Citra MMJ entry (or Android/data/org.citra.emu/files) and select citra-emu, or /citra-emu on older Android. Restart the game normally after editing.",
                "Eden" => "Сохранись в игре и закрой эмулятор. Выбери его папку данных с эмулируемым хранилищем. После редактирования запусти игру обычным способом.",
                "melonDS" => "Сохранись в игре и закрой melonDS. Выбери папку файлов .sav рядом с ROM или указанную в настройках melonDS. Состояния эмулятора не поддерживаются. После редактирования запусти игру обычным способом.",
                _ => "Сохранись в игре и закрой эмулятор. Выбери папку обычных сохранений. Состояния эмулятора (save states) не поддерживаются. После редактирования запусти игру обычным способом.",
            };
            var proceed = await PadMenu.ShowAsync(_hostGrid, $"Подключить {choice}",
                guidance + " If Android does not offer access to the folder, use Single save file with an exported save.",
                new PadOption("Выбрать папку", IconPath: "folder"), new PadOption("Назад", IconPath: "back"));
            if (proceed != "Выбрать папку") continue;
            switch (choice)
            {
                case "RetroArch": await _viewModel.AddRetroArchCommand.ExecuteAsync(null); break;
                case "melonDS": await _viewModel.AddMelonDsCommand.ExecuteAsync(null); break;
                case "Linkboy": await _viewModel.AddLinkboyCommand.ExecuteAsync(null); break;
                case "Azahar / Lime3DS": await _viewModel.AddAzaharCommand.ExecuteAsync(null); break;
                case "Citra MMJ": await _viewModel.AddCitraMmjCommand.ExecuteAsync(null); break;
                case "Eden": await _viewModel.AddEdenCommand.ExecuteAsync(null); break;
                case "Dolphin": await _viewModel.AddDolphinCommand.ExecuteAsync(null); break;
                case "DraStic": await _viewModel.AddDraSticCommand.ExecuteAsync(null); break;
                case "Pizza Boy A (GBA)": await _viewModel.AddPizzaBoyGbaCommand.ExecuteAsync(null); break;
                case "Pizza Boy C (GB/GBC)": await _viewModel.AddPizzaBoyGbcCommand.ExecuteAsync(null); break;
            }
            return;
        }
    }

    private async Task ShowSettingsAsync()
    {
        var hiddenCount = _viewModel.HiddenSaves.Count;
        var hiddenOption = $"Показать скрытые сохранения ({hiddenCount})";
        var options = new List<PadOption>
        {
            new("Подключить эмулятор", IconPath: "link", Detail: "Выбрать папку эмулятора с сохранениями игр."),
            new("Подключённые папки", IconPath: "sdcard", Detail: "Отключить папки эмуляторов. Файлы останутся на устройстве."),
            new("Открыть сохранение", IconPath: "file", Detail: "Выбрать отдельный файл сохранения."),
        };
        if (hiddenCount > 0) options.Add(new PadOption(hiddenOption, IconPath: "show", Detail: "Сохранения, скрытые с главного экрана. Выбери, чтобы вернуть."));
        var scheme = $"Цветовая тема: {PKForge.Chrome.ColorTheme.Current.Name}";
        var background = $"Фон боксов: {BoxBackground.Name(BoxBackground.Style)}";
        options.AddRange(
        [
            new PadOption(scheme, IconPath: "palette", Detail: "Цвета приложения: стандартная синяя тема или тема для каждого типа."),
            new PadOption(background, IconPath: "box", Detail: "Способ отображения игровых обоев в боксах."),
            new PadOption("Точки восстановления", IconPath: "history", Detail: "Резервные копии перед каждой записью для возврата к прежнему состоянию."),
            new PadOption("О PKForge", IconPath: "info"),
            new PadOption("Проверить обновления", IconPath: "update"),
            new PadOption("Музыка", IconPath: "music", Detail: "Воспроизводить свои аудиофайлы в фоне."),
            new PadOption("Разное", IconPath: "gears", Detail: "Спрайты, поиск игр, журналы, экран и режимы редактирования."),
            new PadOption("Выйти из PKForge", IconPath: "quit"),
        ]);
        var choice = await PadMenu.ShowAsync(_hostGrid, "Настройки", null, [.. options]);
        if (choice == hiddenOption) { await ShowHiddenSavesAsync(); return; }
        if (choice == scheme) { await ShowColorSchemeAsync(); return; }
        if (choice == background) { await ShowBoxBackgroundAsync(); return; }
        switch (choice)
        {
            case "Подключить эмулятор": await ShowLinkMenuAsync(); break;
            case "Подключённые папки": await ShowManageLinkedStorageAsync(); break;
            case "Открыть сохранение": await LinkFileAsync(); break;
            case "Точки восстановления": await PushAsync<BackupHistoryPage>(); break;
            case "О PKForge": await AboutPopup.ShowAsync(_hostGrid); break;
            case "Проверить обновления": await CheckForUpdateAsync(automatic: false); break;
            case "Музыка": await ShowMusicAsync(); break;
            case "Разное": await ShowMiscAsync(); break;
            case "Выйти из PKForge":
                if (await PadMenu.ConfirmAsync(_hostGrid, "Выйти из PKForge?", "Перед записью каждого изменения создаётся резервная копия.", "Выйти"))
                    Microsoft.Maui.Controls.Application.Current?.Quit();
                break;
        }
    }

    /// <summary>Hidden saves, one row each; picking one puts it back on the shelf.</summary>
    private async Task ShowHiddenSavesAsync()
    {
        while (_viewModel.HiddenSaves is { Count: > 0 } hidden)
        {
            var save = await SavePickerSheet.ChooseFromTileAsync(_hostGrid, hidden, "Скрытые сохранения",
                "Выбери сохранение для возврата на главный экран и в список выбора.", allowSingle: true);
            if (save is null) return;
            var current = _viewModel.Identities.Get(save.DocumentId) ?? new SaveIdentity(save.DocumentId);
            _viewModel.Identities.Set(current with { Hidden = false });
        }
    }

    private static string IconFor(EmulatorKind kind) => kind switch
    {
        EmulatorKind.RetroArch => "retroarch",
        EmulatorKind.MelonDS => "melonds",
        EmulatorKind.Linkboy => "linkboy",
        EmulatorKind.Azahar or EmulatorKind.CitraMmj => "azahar",
        EmulatorKind.Eden => "eden",
        EmulatorKind.Dolphin => "dolphin",
        EmulatorKind.DraStic => "drastic",
        EmulatorKind.PizzaBoyGba or EmulatorKind.PizzaBoyGbc => "pizzaboy",
        _ => "storage",
    };

    private async Task ShowManageLinkedStorageAsync()
    {
        var roots = IPlatformApplication.Current!.Services.GetRequiredService<IWatchedRootStore>().GetRoots();
        if (roots.Count == 0)
        {
            _viewModel.Status = "Нет подключённых хранилищ.";
            return;
        }

        var options = roots
            .Select(root => new PadOption($"Отключить {SaveDescriptions.EmulatorName(root.Kind)} · {root.DisplayName}", IconPath: IconFor(root.Kind)))
            .Append(new PadOption("Отключить все папки", IconPath: "unlink", Detail: "Отключить все папки. Файлы останутся на устройстве."))
            .Append(new PadOption("Отмена", IconPath: "close"))
            .ToArray();
        var choice = await PadMenu.ShowAsync(_hostGrid, "Подключённые хранилища", "Отключить папку эмулятора без сброса приложения.", options);
        if (choice is null or "Отмена") return;

        var store = IPlatformApplication.Current!.Services.GetRequiredService<IWatchedRootStore>();
        if (choice == "Отключить все папки")
        {
            var confirmedAll = await PadMenu.ConfirmAsync(_hostGrid, "Отключить все хранилища?",
                "Все папки эмуляторов будут отключены от PKForge. Файлы останутся на устройстве.", "Отключить всё");
            if (!confirmedAll) return;
            foreach (var root in roots)
                store.RemoveRoot(root);
            _viewModel.Status = "Все хранилища отключены.";
        }
        else
        {
            var index = Array.FindIndex(options, option => option.Label == choice);
            if (index < 0 || index >= roots.Count) return;
            var root = roots[index];
            var confirmed = await PadMenu.ConfirmAsync(_hostGrid, "Отключить хранилище?",
                $"Отключить {SaveDescriptions.EmulatorName(root.Kind)} · {root.DisplayName} от PKForge? Файлы останутся на устройстве.", "Отключить");
            if (!confirmed) return;
            store.RemoveRoot(root);
            _viewModel.Status = $"{SaveDescriptions.EmulatorName(root.Kind)} отключено.";
        }

        await _viewModel.RescanCommand.ExecuteAsync(null);
    }

    /// <summary>Release checks: automatic failures stay quiet, manual failures explain themselves.</summary>
    private async Task CheckForUpdateAsync(bool automatic)
    {
        var service = IPlatformApplication.Current?.Services.GetService<AppUpdateService>();
        if (service is null) return;

        AppUpdateCheck check;
        try
        {
            check = await service.CheckAsync();
        }
        catch (Exception error)
        {
            AppLog.Warn("update", $"Automatic update check failed: {error}");
            if (!automatic)
                _viewModel.Status = $"Ошибка проверки обновлений: {error.Message}";
            return;
        }

        if (check.Update is not { } update || !check.IsAvailable)
        {
            if (!automatic)
                await DialogueBox.ShowSequenceAsync(_hostGrid, check.Message);
            return;
        }
        if (automatic && !service.ShouldPromptAutomatically(update))
            return;
        if (automatic && !_isAppearing)
            return;

        var choice = await DialogueChoiceBox.ShowAsync(_hostGrid,
            $"A new version of PKForge is available! Version {update.Version} is ready to install. Would you like to update now?",
            "ДА", "НЕТ", "DON'T REMIND ME");
        switch (choice)
        {
            case "ДА":
                await InstallUpdateAsync(update);
                break;
            case "НЕТ":
                _viewModel.Status = $"Version {update.Version} will be offered again next launch.";
                break;
            case "DON'T REMIND ME":
                service.DontRemindMe(update);
                _viewModel.Status = $"I won't remind you about version {update.Version} again.";
                break;
        }
    }

    private async Task InstallUpdateAsync(AvailableAppUpdate update)
    {
        var service = IPlatformApplication.Current!.Services.GetRequiredService<AppUpdateService>();
        var overlay = LoadingOverlay.Show(_hostGrid, "Downloading the update!",
            $"PKForge {update.Version} is on its way. Android will confirm the installation.");
        AppUpdateInstallResult result;
        try
        {
            result = await service.DownloadAndInstallAsync(update, (done, total) =>
            {
                var percent = total <= 0 ? 0 : (int)Math.Clamp(done * 100 / total, 0, 100);
                overlay.Report(percent, 100);
            }, overlay.Cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            _viewModel.Status = "Update paused. You can start it again from Settings.";
            return;
        }
        catch (Exception error)
        {
            AppLog.Warn("update", $"Download/install failed: {error}");
            _viewModel.Status = $"Ошибка обновления: {error.Message}";
            var openRelease = await PadMenu.ConfirmAsync(_hostGrid, "Open the release page?",
                "The in-app installer could not finish. The GitHub release page has the same APK.", "Открыть");
            if (openRelease)
                await Launcher.OpenAsync(update.ReleaseUrl);
            return;
        }
        finally
        {
            overlay.Close();
        }

        switch (result)
        {
            case AppUpdateInstallResult.InstallerOpened:
                _viewModel.Status = "Android is installing the update.";
#if ANDROID
                // Android answers later, through the install receiver; a refusal is shown here.
                void OnFailed(string reason)
                {
                    UpdateInstallReceiver.Failed -= OnFailed;
                    _ = ReportInstallFailureAsync(update, reason);
                }
                UpdateInstallReceiver.Failed += OnFailed;
#endif
                break;
            case AppUpdateInstallResult.ReleasePageOpened:
                _viewModel.Status = "The PKForge release page is open.";
                break;
            case AppUpdateInstallResult.InstallPermissionRequired:
                _pendingAuthorizedUpdate = update;
                var permissionChoice = await DialogueChoiceBox.ShowAsync(_hostGrid,
                    "Android needs permission to install PKForge updates. Allow install unknown apps for PKForge, then check again?",
                    "OPEN SETTINGS", "НЕ СЕЙЧАС");
                if (permissionChoice == "OPEN SETTINGS")
                    AppUpdateService.OpenInstallPermissionSettings();
                break;
        }
    }

    private async Task ReportInstallFailureAsync(AvailableAppUpdate update, string reason)
    {
        _viewModel.Status = $"Ошибка обновления: {reason}";
        var openRelease = await PadMenu.ConfirmAsync(_hostGrid, "Open the release page?",
            $"Android did not install the update ({reason}). The GitHub release page has the same APK.", "Открыть");
        if (openRelease)
            await Launcher.OpenAsync(update.ReleaseUrl);
    }

    private Task ShowMusicAsync() => MusicMenu.ShowAsync(_hostGrid, text => _viewModel.Status = text);

    /// <summary>How each PC box shows its game wallpaper.</summary>
    private async Task ShowBoxBackgroundAsync()
    {
        var styles = Enum.GetValues<PKForge.Chrome.StoragePaint.WallpaperStyle>();
        var picked = await PadMenu.ShowAsync(_hostGrid, "Фон боксов", null,
            [
                new PadOption(BoxBackground.Name(PKForge.Chrome.StoragePaint.WallpaperStyle.Blue), Detail: "Узор игровых обоев в цветах выбранной темы."),
                new PadOption(BoxBackground.Name(PKForge.Chrome.StoragePaint.WallpaperStyle.Veiled), Detail: "Игровые обои под оттенком выбранной темы."),
                new PadOption(BoxBackground.Name(PKForge.Chrome.StoragePaint.WallpaperStyle.Duotone), Detail: "Узор обоев в двух оттенках: темы и самих обоев."),
                new PadOption(BoxBackground.Name(PKForge.Chrome.StoragePaint.WallpaperStyle.Horizon), Detail: "Обои появляются снизу и плавно переходят в цвета темы."),
            ]);
        foreach (var style in styles.Where(style => BoxBackground.Name(style) == picked))
        {
            BoxBackground.Set(style);
            _viewModel.Status = $"Фон боксов: {BoxBackground.Name(style)}.";
        }
    }

    /// <summary>The color scheme picker; a new scheme rebuilds Home and the lower screen in its colors.</summary>
    private async Task ShowColorSchemeAsync()
    {
        var theme = await ThemePicker.ShowAsync(_hostGrid);
        if (theme is null || theme.Id == PKForge.Chrome.ColorTheme.Current.Id) return;
        ColorThemeSetting.Set(theme);
        AppLog.Info("theme", $"Player picked the {theme.Id} color scheme");
        if (Application.Current is App app) await app.ReloadForThemeAsync();
    }

    /// <summary>Maintenance actions: the sprite pack, the rescan, and the scan report.</summary>
    private async Task ShowMiscAsync()
    {
        var trainerProfiles = IPlatformApplication.Current!.Services.GetRequiredService<TrainerProfileStore>();
        var choice = await PadMenu.ShowAsync(_hostGrid, "Разное", null,
            new PadOption(trainerProfiles.UseCurrentTrainerForGeneration
                ? "Подчинение тренеру: ВКЛ"
                : "Подчинение тренеру: ВЫКЛ", IconPath: "profile",
                Detail: "Созданные покемоны будут принадлежать тренеру из открытого сохранения."),
            new PadOption($"Скачать все спрайты ({SpritePackDownloader.SizeHint})", IconPath: "download", Detail: "Анимации, изображения HOME и значки предметов для работы без сети."),
            new PadOption("Найти игры заново", IconPath: "refresh", Detail: "Повторно найти сохранения в подключённых папках."),
            new PadOption("Отчёт поиска", IconPath: "report", Detail: "Результаты последнего поиска по папкам. Отчёт можно скопировать."),
            new PadOption("Поделиться журналами", IconPath: "export", Detail: "Отчёты об ошибках и последних действиях для диагностики."),
            new PadOption(SecondScreenMode.UserOff ? "Второй экран: ВЫКЛ" : "Второй экран: ВКЛ", IconPath: "compact",
                Detail: "OFF keeps PKForge on one screen, so the other stays free (an emulator, say)."),
            new PadOption(Services.HaXMode.IsOn ? "Режим HaX: ВКЛ" : "Режим HaX: ВЫКЛ", IconPath: "hax",
                Detail: "Разрешает любые значения, в том числе нелегальные."),
            new PadOption(Services.HardcoreMode.IsOn ? "Режим Hardcore: ВКЛ" : "Режим Hardcore: ВЫКЛ", IconPath: "hardcore",
                Detail: "Блокирует изменения, создание и копирование. Перемещение остаётся доступным."));
        switch (choice)
        {
            case var ownership when ownership?.StartsWith("Подчинение тренеру:", StringComparison.Ordinal) == true:
                trainerProfiles.SetUseCurrentTrainerForGeneration(!trainerProfiles.UseCurrentTrainerForGeneration);
                _viewModel.Status = trainerProfiles.UseCurrentTrainerForGeneration
                    ? "Созданные покемоны будут принадлежать тренеру из открытого сохранения."
                    : "При создании могут использоваться данные тренера Auto-Legality.";
                break;
            case var pack when pack?.StartsWith("Скачать все спрайты", StringComparison.Ordinal) == true:
                await DownloadSpritePackAsync();
                break;
            case "Найти игры заново":
                if (_viewModel.IsBusy && !_viewModel.ReleaseIfStuck())
                {
                    await PadMenu.ShowAsync(_hostGrid, "Операция выполняется",
                        $"PKForge выполняет операцию \u00AB{_viewModel.BusyWith}\u00BB. Подожди; если она зависла, перезапусти приложение.", "OK");
                    break;
                }
                // Called directly: the command refuses to start while an older run it owns is still pending.
                await _viewModel.RescanAsync();
                break;
            case "Поделиться журналами":
                await ShareLogsAsync();
                break;
            case "Второй экран: ВКЛ" or "Второй экран: ВЫКЛ":
            {
                var turnOff = !SecondScreenMode.UserOff;
                SecondScreenMode.SetUserOff(turnOff);
                var host = IPlatformApplication.Current?.Services.GetService<ISecondaryDisplayHost>();
                try
                {
                    if (turnOff) { if (host is not null) await host.DismissAsync(); }
                    else if (host?.IsAvailable == true) await host.ShowAsync();
                }
                catch (InvalidOperationException error)
                {
                    AppLog.Warn("second", $"Toggling the lower screen: {error.Message}");
                }
                AppLog.Info("second", turnOff ? "Player turned the second screen off" : "Player turned the second screen on");
                _viewModel.Status = turnOff
                    ? "Второй экран выключен."
                    : host?.IsAvailable == true ? "Второй экран включён." : "Второй экран включён, но дополнительный дисплей недоступен.";
                break;
            }
            case "Отчёт поиска":
            {
                var action = await PadMenu.ShowAsync(_hostGrid, "Отчёт поиска", _viewModel.ScanReport, "Копировать отчёт", "Закрыть");
                if (action == "Копировать отчёт")
                {
                    await Clipboard.Default.SetTextAsync(_viewModel.ScanReport);
                    _viewModel.Status = "Отчёт поиска скопирован";
                }
                break;
            }
            case "Режим HaX: ВЫКЛ":
                var on = await PadMenu.ConfirmAsync(_hostGrid, "Включить режим HaX?",
                    "Pickers will offer every option instead of the legal subset (any ability on any mon). " +
                    "Такие изменения могут сделать покемонов нелегальными.", "Включить");
                if (on) { Services.HaXMode.Set(true); _viewModel.Status = "Режим HaX включён."; }
                break;
            case "Режим HaX: ВКЛ":
                Services.HaXMode.Set(false);
                _viewModel.Status = "Режим HaX выключен.";
                break;
            case "Режим Hardcore: ВЫКЛ":
                if (await PadMenu.ConfirmAsync(_hostGrid, "Включить режим Hardcore?", Services.HardcoreMode.SettingExplanation, "Включить"))
                {
                    Services.HardcoreMode.Set(true);
                    _viewModel.Status = $"{Services.HardcoreMode.Marker}: Hardcore mode is ON - moves only, no edits or copies.";
                }
                break;
            case "Режим Hardcore: ВКЛ":
                Services.HardcoreMode.Set(false);
                _viewModel.Status = "Режим Hardcore выключен.";
                break;
        }
    }

    /// <summary>
    /// Zips the logs, crash reports, device details and scan report, then opens the share
    /// sheet. Nothing is sent anywhere unless the player picks where it goes.
    /// </summary>
    private async Task ShareLogsAsync()
    {
        try
        {
            var archive = await Task.Run(() => AppLog.BuildShareArchive(new Dictionary<string, string>
            {
                ["scan-report.txt"] = _viewModel.ScanReport,
            }));
            AppLog.MarkCrashesSeen();
            await Share.Default.RequestAsync(new ShareFileRequest("Журналы PKForge", new ShareFile(archive, "application/zip")));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            AppLog.Error("logs", "Не удалось отправить журналы", error);
            await PadMenu.ShowAsync(_hostGrid, "Поделиться журналами", $"Не удалось собрать журналы: {error.Message}", "OK");
        }
    }

    private static bool _crashOffered;

    /// <summary>After a crash, offers the report once on the next launch.</summary>
    private async Task OfferCrashReportAsync()
    {
        if (AppLog.UnseenCrashes().Count == 0) return;
        var choice = await PadMenu.ShowAsync(_hostGrid, "PKForge завершился с ошибкой",
            "Отчёт об ошибке сохранён. Он содержит ошибку, недавние действия, имена файлов и модель устройства, без данных сохранений.",
            "Поделиться отчётом", "Не сейчас");
        AppLog.MarkCrashesSeen();
        if (choice == "Поделиться отчётом") await ShareLogsAsync();
    }

    /// <summary>Downloads every species' animated + HOME sprites for full offline use.</summary>
    private async Task DownloadSpritePackAsync()
    {
        var downloader = IPlatformApplication.Current?.Services.GetService<SpritePackDownloader>();
        if (downloader is null) return;
        if (await Task.Run(downloader.CountMissing) == 0)
        {
            await PadMenu.ShowAsync(_hostGrid, "Набор спрайтов", "Все спрайты уже загружены и доступны без сети.", "OK");
            return;
        }
        var overlay = LoadingOverlay.Show(_hostGrid, "Собираем все спрайты!",
            "Загрузка анимаций и изображений HOME для всех покемонов. Можно прервать: загруженные части сохранятся.");
        try
        {
            await downloader.RunAsync(overlay.Report, overlay.Cancellation.Token);
            _viewModel.Status = "Спрайты загружены и доступны без сети.";
        }
        catch (OperationCanceledException)
        {
            _viewModel.Status = "Загрузка спрайтов приостановлена. Продолжить можно в настройках.";
        }
        catch (Exception error)
        {
            _viewModel.Status = $"Загрузка спрайтов остановлена: {error.Message}";
        }
        finally
        {
            overlay.Close();
        }
    }

    private async Task LinkFileAsync()
    {
        await _viewModel.LinkFileCommand.ExecuteAsync(null);
        if (_viewModel.OpenedSave)
            await PushAsync<BoxBrowserPage>();
        else if (_viewModel.Status.StartsWith("Не удалось", StringComparison.Ordinal))
            await PadMenu.ShowAsync(_hostGrid, "Сохранение не открылось", _viewModel.Status, "OK");
    }

    /// <summary>
    /// A game as an actual cartridge: fixed uniform size, dark contact strip on top,
    /// and the game art as the cart's label sticker (era-colored plastic behind it).
    /// </summary>
    private const double TilePadding = 8;
    private const double TileInnerWidth = 128;

    private View BuildCartridgeTile()
    {
        const double cartWidth = 76;
        const double cartHeight = 84;

        var contactStrip = new BoxView { HeightRequest = 9, Color = UiTokens.MaroonDeep };

        var gameMark = new GameCartridgeMark();
        gameMark.SetBinding(GameCartridgeMark.GenerationProperty, nameof(DetectedSave.Generation));

        // Use the real game label art whenever we bundle it. The identity mark remains a
        // deliberate, consistent fallback for homebrew, hacks, and unknown saves.
        var gameArt = new Image
        {
            Aspect = Aspect.AspectFit,
            IsVisible = false,
            Margin = new Thickness(3),
        };
        gameArt.BindingContextChanged += async (_, _) =>
        {
            if (gameArt.BindingContext is not SaveCard group) return;
            gameArt.IsVisible = false;
            gameMark.IsVisible = true;
            // Box art follows the game, never the custom name; unnamed hacks keep the mark.
            var path = group.Save.ArtLabel is { } art ? await GameArt.GetIconAsync(art) : null;
            // A recycled template may have moved on while package I/O was in flight.
            if (!ReferenceEquals(gameArt.BindingContext, group)) return;
            gameArt.Source = path;
            gameArt.IsVisible = path is not null;
            gameMark.IsVisible = path is null;
        };
        var sticker = new Border
        {
            BackgroundColor = UiTokens.ShellPress,
            Stroke = UiTokens.Outline,
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 4 },
            Margin = new Thickness(7, 5, 7, 7),
            Content = new Grid { Children = { gameMark, gameArt } },
        };

        var cartLayout = new Grid
        {
            RowDefinitions = [new(GridLength.Auto), new(GridLength.Star)],
            Children = { contactStrip, sticker },
        };
        Grid.SetRow(sticker, 1);

        var cartBody = new Border
        {
            WidthRequest = cartWidth,
            HeightRequest = cartHeight,
            StrokeThickness = 1.5,
            StrokeShape = new RoundRectangle { CornerRadius = 7 },
            HorizontalOptions = LayoutOptions.Center,
            Padding = 0,
            Content = cartLayout,
        };
        // The plastic is the player's chosen color, else the console era's.
        cartBody.SetBinding(BackgroundColorProperty, new Binding(".", converter: CartColor));
        cartBody.SetBinding(Border.StrokeProperty, new Binding(".", converter: CartEdge));

        // Several saves of one game read as a stack of physical carts: up to two plain
        // carts in the same plastic sit askew behind the labelled one (max 3 layers).
        Border StackedCart(double rotation, double dx, double dy, string visibleWhen)
        {
            var back = new Border
            {
                WidthRequest = cartWidth,
                HeightRequest = cartHeight,
                StrokeThickness = 1.5,
                StrokeShape = new RoundRectangle { CornerRadius = 7 },
                HorizontalOptions = LayoutOptions.Center,
                Padding = 0,
                Rotation = rotation,
                TranslationX = dx,
                TranslationY = dy,
                InputTransparent = true,
                Content = new BoxView { HeightRequest = 9, Color = UiTokens.MaroonDeep, VerticalOptions = LayoutOptions.Start },
            };
            back.SetBinding(BackgroundColorProperty, new Binding(".", converter: CartColor));
            back.SetBinding(Border.StrokeProperty, new Binding(".", converter: CartEdge));
            back.SetBinding(IsVisibleProperty, visibleWhen);
            return back;
        }

        var iconHost = new Grid
        {
            WidthRequest = TileInnerWidth,
            HeightRequest = cartHeight,
            HorizontalOptions = LayoutOptions.Center,
            Children =
            {
                StackedCart(-9, -13, 0, nameof(SaveCard.HasManySaves)),
                StackedCart(8, 13, -1, nameof(SaveCard.HasSeveralSaves)),
                cartBody,
            },
        };

        // Every line gets the tile's full inner width so long names end in an ellipsis
        // instead of being clipped on both sides of a centred run.
        Label ShelfLine(double size, Color color, bool bold = false) => new()
        {
            TextColor = color,
            FontSize = size,
            FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None,
            HorizontalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 1,
            WidthRequest = TileInnerWidth,
            // The pixel font's first glyph overhangs its advance; keep it off the clip edge.
            Padding = new Thickness(3, 0),
        };

        var name = ShelfLine(UiTokens.TextLabel, UiTokens.Ink0);
        name.FontFamily = DsChrome.PixelFont;
        name.SetBinding(Label.TextProperty, nameof(SaveCard.ShelfTitle));

        var trainer = ShelfLine(UiTokens.TextSmall, UiTokens.Ink1);
        trainer.SetBinding(Label.TextProperty, nameof(SaveCard.TrainerLine));

        // Emulator · folder · date: what tells two saves of one game apart at a glance.
        var detail = ShelfLine(12, UiTokens.InkSoft);
        detail.SetBinding(Label.TextProperty, nameof(SaveCard.DetailLine));

        // Every tile is exactly the same size: the shelf must read as a row of carts.
        // A stack also carries a solid "×N" plate on the free right of the carts, fully
        // inside the tile so it never clips.
        var countLabel = new Label
        {
            TextColor = UiTokens.Ink0,
            FontSize = UiTokens.TextSmall,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
        };
        countLabel.SetBinding(Label.TextProperty, nameof(SaveCard.CountBadge));
        var countBadge = new Border
        {
            BackgroundColor = UiTokens.MaroonDeep,
            Stroke = UiTokens.Outline,
            StrokeThickness = 1.5,
            StrokeShape = new RoundRectangle { CornerRadius = 3 },
            HeightRequest = 22,
            MinimumWidthRequest = 30,
            Padding = new Thickness(5, 0),
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.End,
            Margin = new Thickness(0, 0, 0, 2),
            InputTransparent = true,
            Content = countLabel,
        };
        countBadge.SetBinding(IsVisibleProperty, nameof(SaveCard.HasSeveralSaves));
        iconHost.Children.Add(countBadge);

        var card = Kit.DevicePanel(new VerticalStackLayout
        {
            Spacing = 3,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Children = { iconHost, name, trainer, detail },
        }, padding: TilePadding);
        card.WidthRequest = TileInnerWidth + 2 * TilePadding;
        card.HeightRequest = 166;

        card.Triggers.Add(new DataTrigger(typeof(Border))
        {
            Binding = new Binding(nameof(SaveCard.IsSelected)),
            Value = true,
            Setters =
            {
                // The selected look: cobalt body, pale 2 dp edge - not a cyan glow ring.
                new Setter { Property = Border.BackgroundProperty, Value = new SolidColorBrush(UiTokens.SelectFill) },
                new Setter { Property = Border.StrokeProperty, Value = UiTokens.Ink0.WithAlpha(0.85f) },
            },
        });
        void Select(SaveCard group)
        {
            _shelfIndex = _viewModel.Groups.IndexOf(group);
            for (var i = 0; i < _viewModel.Groups.Count; i++)
                _viewModel.Groups[i].IsSelected = i == _shelfIndex;
        }

        // A tap opens the save; a long press (held ~0.5 s) opens its identity menu instead.
        AttachPress(card,
            () =>
            {
                if (card.BindingContext is not SaveCard group) return;
                Select(group);
                _ = OpenCardAsync(group);
            },
            () =>
            {
                if (card.BindingContext is not SaveCard group) return;
                Select(group);
                _ = ShowSaveMenuAsync(group);
            });
        BlockNativeFocus(card);
        return card;
    }

    /// <summary>The Living Dex Autopilot over every save on the shelf and the Bank.</summary>
    private async Task OpenAutopilotAsync()
    {
        var services = IPlatformApplication.Current?.Services;
        var boxes = services?.GetService<BoxBrowserViewModel>();
        var data = services?.GetService<IGameDataService>();
        var sprites = services?.GetService<ISpriteService>();
        if (boxes is null || data is null || sprites is null) return;
        await LivingDexAutopilotPage.ShowAsync(_hostGrid, boxes, data, sprites);
        // Pokémon moved between games: the shelf's counts and the open save follow.
        _viewModel.Status = boxes.Status;
    }

    /// <summary>The events shelf: community collections here, wondercards inside the game.</summary>
    private async Task ShowEventsMenuAsync()
    {
        var choice = await PadMenu.ShowAsync(_hostGrid, "База событий", null,
            new PadOption($"Боксы сообщества ({Services.CommunityBoxService.RepoTitle})", IconPath: "community",
                Detail: "Коллекции событийных покемонов для добавления в банк."),
            new PadOption("Карточки подарков", IconPath: "events", Detail: "Открой игру, нажми Y и выбери карточки подарков."));
        switch (choice)
        {
            case var boxes when boxes?.StartsWith("Боксы сообщества", StringComparison.Ordinal) == true:
                await CollectionCenter.ShowAsync(_hostGrid);
                break;
            case "Карточки подарков":
                await PadMenu.ShowAsync(_hostGrid, "Карточки подарков",
                    "Подарки зависят от игры. Открой игру, нажми Y (данные сохранения) и выбери карточки подарков.", "OK");
                break;
        }
    }

    private static readonly IValueConverter CartColor = new FuncConverter(card =>
        card is SaveCard c ? SaveColors.For(c.ColorKey, c.Generation) : Kit.EraColor(0));
    private static readonly IValueConverter CartEdge = new FuncConverter(card =>
        (card is SaveCard c ? SaveColors.For(c.ColorKey, c.Generation) : Kit.EraColor(0)).AddLuminosity(-0.15f));

    private sealed class FuncConverter(Func<object?, Color> convert) : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => convert(value);
        public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
    }

    private async Task OpenSaveAsync(DetectedSave save)
    {
        if (!await ChooseEditionOnceAsync(save)) return;
        // The pick re-resolves the save: open the version that carries the chosen edition.
        save = _viewModel.Saves.FirstOrDefault(s => s.DocumentId == save.DocumentId) ?? save;
        if (save.RequiresExtraCare)
        {
            var confirmed = await PadMenu.ConfirmAsync(_hostGrid,
                "Emulated console storage",
                $"{save.GameLabel} lives inside {save.Emulator}'s emulated storage - the delicate path. " +
                "PKForge создаёт копию перед записью. Перед редактированием закрой эмулятор.",
                "Подключить");
            if (!confirmed) return;
        }

        await _viewModel.OpenAsync(save);
        if (_viewModel.OpenedSave)
        {
            // PreviewGame stays: Home's claim is released while the box is in front, and
            // coming back shows the game just left instead of blank branding.
            await PushAsync<BoxBrowserPage>();
        }
        else if (_viewModel.Status.StartsWith("Не удалось", StringComparison.Ordinal))
            await PadMenu.ShowAsync(_hostGrid, "Сохранение не открылось", _viewModel.Status, "OK");
    }

    private async Task PushAsync<TPage>() where TPage : Page
    {
        var services = IPlatformApplication.Current?.Services
            ?? throw new InvalidOperationException("Службы приложения недоступны.");
        await Navigation.PushAsync(services.GetRequiredService<TPage>());
    }

    private async Task PushParkAsync()
    {
        if (Interlocked.Exchange(ref _parkNavigationPending, 1) != 0) return;
        try
        {
            var services = IPlatformApplication.Current?.Services
                ?? throw new InvalidOperationException("Службы приложения недоступны.");
            _parkPage ??= services.GetRequiredService<PokeparkPage>();
            await Navigation.PushAsync(_parkPage);
        }
        finally
        {
            Volatile.Write(ref _parkNavigationPending, 0);
        }
    }
}

internal static class ViewBuilderExtensions
{
    public static T Also<T>(this T view, Action<T> configure) where T : View
    {
        configure(view);
        return view;
    }
}
