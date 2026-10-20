using PKForge.Domain;

namespace PKForge.App.Views;

/// <summary>
/// Background music, the same menu from Home and from the Poképark's options: play/pause, skip,
/// the music of each place (the menus, the park by day, the park by night), the library, order,
/// autostart.
/// </summary>
public static class MusicMenu
{
    private static readonly (MusicPlace Place, string Name, string Detail)[] Places =
    [
        (MusicPlace.Menus, "Меню", "Музыка во всём приложении, кроме Покепарка."),
        (MusicPlace.ParkDay, "Парк днём", "Музыка в Покепарке утром и днём."),
        (MusicPlace.ParkNight, "Парк ночью", "Музыка в Покепарке вечером и ночью."),
    ];

    public static async Task ShowAsync(Grid host, Action<string> status)
    {
        var music = IPlatformApplication.Current?.Services.GetService<IMusicPlayer>();
        if (music is null) return;
        var start = 0;

        while (true)
        {
            var order = music.Order == MusicOrder.Shuffle ? "Случайно" : "По порядку";
            var auto = music.Autostart ? "ВКЛ" : "ВЫКЛ";
            var message = music.IsPlaying && music.CurrentIndex is { } index && index < music.Library.Count
                ? $"Сейчас играет: {music.Library[index].Title}"
                : music.IsOn && music.Library.Count > 0 ? $"Здесь тишина ({PlaceName(music.Place)})."
                : $"Медиатека: треков {music.Library.Count}";
            if (music.Library.Count == 0)
                message += "\nДобавь свои аудиофайлы кнопкой «Добавить музыку». Встроенных треков нет.";
#if ANDROID
            if (music is Platforms.Android.MusicPlayer { LastError: { } error })
                message += $"\nОшибка: {error}";
#elif WINDOWS
            if (music is Platforms.Windows.DesktopMusicPlayer { LastError: { } error })
                message += $"\nОшибка: {error}";
#endif
            PadOption[] options =
            [
                new(music.IsOn ? "Пауза" : "Воспроизвести", IconPath: music.IsOn ? "pause" : "play"),
                new("Следующий трек", IconPath: "skip"),
                .. Places.Select(p => new PadOption($"{p.Name}: {Describe(music, p.Place)}",
                    IconPath: p.Place == MusicPlace.Menus ? "music" : "park", Detail: p.Detail)),
                new($"Добавить музыку ({music.Library.Count})", IconPath: "folder", Detail: "Выбери аудиофайлы на устройстве для добавления в медиатеку."),
                new(music.Library.Count > 0 ? "Очистить медиатеку" : "-", IconPath: "delete",
                    Detail: music.Library.Count > 0 ? "Очистить список. Аудиофайлы останутся на устройстве." : null),
                new($"Порядок: {order}", IconPath: "shuffle", Detail: "Воспроизводить треки по порядку или случайно."),
                new($"Автозапуск: {auto}", IconPath: "settings", Detail: "При включении музыка запускается вместе с PKForge."),
            ];
            var choice = await PadMenu.ShowAtAsync(host, "Фоновая музыка", message, start, options);
            start = Math.Max(0, Array.FindIndex(options, o => o.Label == choice));
            if (Places.FirstOrDefault(p => choice?.StartsWith(p.Name + ": ", StringComparison.Ordinal) == true) is { Name: not null } place)
            {
                await PickAsync(host, music, place.Place, status);
                continue;
            }
            switch (choice)
            {
                case "Воспроизвести":
                    if (music.Library.Count == 0) goto case "Добавить музыку";
                    music.Play(); break;
                case "Пауза": music.Pause(); break;
                case "Следующий трек": music.Skip(); break;
                case "Добавить музыку":
                case var add when add?.StartsWith("Добавить музыку", StringComparison.Ordinal) == true:
                {
                    var picker = IPlatformApplication.Current?.Services.GetService<IDocumentPicker>();
                    if (picker is null) break;
                    var documents =
#if WINDOWS
                        await WindowsDocumentPicker.PickMusicAsync();
#else
                        await picker.PickManyAsync();
#endif
                    if (documents.Count == 0) break;
                    var added = music.Add(documents);
                    status(added > 0 ? $"Добавлено треков: {added}." : "Эти файлы уже в медиатеке.");
                    if (added > 0) music.Play();
                    break;
                }
                case "Очистить медиатеку":
                    var confirmed = await PadMenu.ConfirmAsync(host, "Очистить медиатеку?",
                        "Очистить список треков. Аудиофайлы не удаляются.", "Очистить");
                    if (confirmed) music.Clear();
                    break;
                case var o when o?.StartsWith("Порядок: ", StringComparison.Ordinal) == true:
                    music.SetOrder(music.Order == MusicOrder.InOrder ? MusicOrder.Shuffle : MusicOrder.InOrder);
                    break;
                case var a when a?.StartsWith("Автозапуск: ", StringComparison.Ordinal) == true:
                    music.SetAutostart(!music.Autostart);
                    break;
                default:
                    return;
            }
        }
    }

    private static string PlaceName(MusicPlace place) => Places.First(p => p.Place == place).Name.ToLowerInvariant();

    private static string Describe(IMusicPlayer music, MusicPlace place)
    {
        var source = music.SourceFor(place);
        var tracks = music.Library.Where(t => source.Tracks.Contains(t.DocumentId)).ToArray();
        return source.Kind switch
        {
            MusicSourceKind.Silence => "Тишина",
            MusicSourceKind.AllTracks => "Все треки",
            MusicSourceKind.Tracks when tracks.Length == 1 => tracks[0].Title,
            MusicSourceKind.Tracks when tracks.Length > 1 => $"Треков: {tracks.Length}",
            _ => place == MusicPlace.Menus ? "Все треки" : "Как в меню",
        };
    }

    /// <summary>
    /// A place's music: the default (the whole library for the menus, the menus' music for the
    /// park), every track, silence, or the tracks ticked in the list. Ticking a track keeps the
    /// menu open on it, so several can be ticked in a row; B goes back.
    /// </summary>
    private static async Task PickAsync(Grid host, IMusicPlayer music, MusicPlace place, Action<string> status)
    {
        var (_, name, detail) = Places.First(p => p.Place == place);
        var start = 0;
        while (true)
        {
            var source = music.SourceFor(place);
            var library = music.Library;
            var titles = UniqueTitles(library);
            var fallback = place == MusicPlace.Menus ? "Все треки" : "Как в меню";
            var ticked = source.Kind == MusicSourceKind.Tracks ? source.Tracks.ToHashSet() : [];
            PadOption Option(string label, bool on, string icon, string? optionDetail = null) =>
                new(label, IconPath: on ? "check" : icon, Detail: optionDetail);
            var options = new List<PadOption>
            {
                Option(fallback, source.Kind == MusicSourceKind.Default || (source.Kind == MusicSourceKind.AllTracks && place == MusicPlace.Menus), "music",
                    place == MusicPlace.Menus ? "Вся медиатека." : "Продолжать музыку меню без прерывания."),
            };
            if (place != MusicPlace.Menus)
                options.Add(Option("Все треки", source.Kind == MusicSourceKind.AllTracks, "music", "Вся медиатека."));
            options.Add(Option("Тишина", source.Kind == MusicSourceKind.Silence, "close", "Здесь без музыки."));
            for (var i = 0; i < library.Count; i++)
                options.Add(Option(titles[i], ticked.Contains(library[i].DocumentId), "music"));
            var message = library.Count == 0
                ? $"{detail} Сначала добавь аудиофайлы в медиатеку."
                : $"{detail} Отметь один или несколько треков.";
            var choice = await PadMenu.ShowAtAsync(host, $"Музыка: {name}", message, start, options.ToArray());
            if (choice is null) return;
            start = Math.Max(0, options.FindIndex(o => o.Label == choice));
            var track = Array.IndexOf(titles, choice);
            if (track >= 0)
            {
                var id = library[track].DocumentId;
                music.SetSource(place, MusicSource.Of(ticked.Contains(id) ? ticked.Where(t => t != id) : ticked.Append(id)));
                continue;
            }
            music.SetSource(place, choice switch
            {
                "Тишина" => MusicSource.Silence,
                "Все треки" when place != MusicPlace.Menus => MusicSource.AllTracks,
                _ => MusicSource.Default,
            });
            status($"{name}: {Describe(music, place)}.");
            return;
        }
    }

    // Menu entries are told apart by their label: two files with one title get a number.
    private static string[] UniqueTitles(IReadOnlyList<MusicTrack> library)
    {
        var seen = new Dictionary<string, int>();
        var titles = new string[library.Count];
        for (var i = 0; i < library.Count; i++)
        {
            var title = library[i].Title;
            seen[title] = seen.GetValueOrDefault(title) + 1;
            titles[i] = seen[title] == 1 && title is not ("Все треки" or "Как в меню" or "Тишина") ? title : $"{title} ({seen[title]})";
        }
        return titles;
    }
}
