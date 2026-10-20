using System.Text.Json;
using PKForge.Domain;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using PKForge.App.Services;

namespace PKForge.App.Platforms.Windows;

public sealed class DesktopMusicPlayer : IMusicPlayer, IDisposable
{
    private readonly MediaPlayer _player = new();
    private readonly List<MusicTrack> _library;
    private readonly IPreferences _preferences;
    private MediaSource? _source;
    private int _loadVersion;
    private bool _loading;
    private bool _disposed;
    private bool _resume;
    public string? LastError { get; private set; }
    public IReadOnlyList<MusicTrack> Library => _library;
    public bool IsPlaying => _player.PlaybackSession.PlaybackState == MediaPlaybackState.Playing;
    public bool IsOn { get; private set; }
    public int? CurrentIndex { get; private set; }
    public MusicPlace Place { get; private set; }
    public MusicOrder Order => Enum.TryParse<MusicOrder>(_preferences.Get("music_order", "InOrder"), out var value) ? value : MusicOrder.InOrder;
    public bool Autostart => _preferences.Get("music_autostart", false);

    public DesktopMusicPlayer() : this(Preferences.Default) { }

    internal DesktopMusicPlayer(IPreferences preferences)
    {
        _preferences = preferences;
        _player.AudioCategory = MediaPlayerAudioCategory.Media;
        _player.Volume = 1;
        _player.IsMuted = false;
        try { _library = JsonSerializer.Deserialize<List<MusicTrack>>(_preferences.Get("desktop_music_library", "[]")) ?? []; }
        catch (JsonException) { _library = []; }
        _player.MediaEnded += (_, _) => MainThread.BeginInvokeOnMainThread(() => { if (!_disposed && IsOn) Skip(); });
        _player.MediaFailed += (_, args) => MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!_disposed && (_source is not null || _loading)) Fail($"Windows не смог воспроизвести файл: {args.ErrorMessage}");
        });
    }

    public int Add(IReadOnlyList<PickedDocument> documents)
    {
        var before = _library.Count;
        foreach (var document in documents)
            if (!_library.Any(t => string.Equals(t.DocumentId, document.DocumentId, StringComparison.OrdinalIgnoreCase)))
                _library.Add(new MusicTrack(document.DocumentId, document.DisplayName));
        Persist();
        return _library.Count - before;
    }

    public void Remove(int index)
    {
        Pause(); _library.RemoveAt(index); ResetSource(); Persist();
    }
    public void Clear() { Pause(); _library.Clear(); ResetSource(); Persist(); }
    private void Persist() => _preferences.Set("desktop_music_library", JsonSerializer.Serialize(_library));
    public void SetOrder(MusicOrder order) => _preferences.Set("music_order", order.ToString());
    public void SetAutostart(bool on) => _preferences.Set("music_autostart", on);
    public MusicSource SourceFor(MusicPlace place) => MusicSource.Parse(_preferences.Get($"music_source_{place}", ""));
    public void SetSource(MusicPlace place, MusicSource source)
    {
        _preferences.Set($"music_source_{place}", source.Serialize());
        if (IsOn) Play();
    }
    public void EnterPlace(MusicPlace place)
    {
        if (Place == place) return;
        Place = place;
        if (IsOn) Play();
    }
    public void Play()
    {
        IsOn = true;
        LastError = null;
        var list = MusicPlaces.Playlist(Place, SourceFor, Library);
        if (list is null)
        {
            ResetSource();
            if (_library.Count == 0) Fail("В медиатеке нет музыки. Нажми «Добавить музыку» и выбери аудиофайлы.");
            return;
        }
        if (CurrentIndex is { } index && list.Contains(index) && (_source is not null || _loading))
        {
            if (!_loading) TryPlay();
        }
        else _ = LoadAsync(list[0]);
    }
    public void Pause() { IsOn = false; _player.Pause(); }
    public void Skip()
    {
        var list = MusicPlaces.Playlist(Place, SourceFor, Library);
        if (list is null) return;
        var position = CurrentIndex is { } index ? Array.IndexOf(list, index) : -1;
        var next = Order == MusicOrder.Shuffle && list.Length > 1
            ? list.Where(i => i != CurrentIndex).OrderBy(_ => Random.Shared.Next()).First()
            : list[(position + 1) % list.Length];
        _ = LoadAsync(next);
    }

    private async Task LoadAsync(int index)
    {
        var version = ++_loadVersion;
        _loading = true;
        CurrentIndex = index;
        LastError = null;
        _player.Pause();
        try
        {
            // StorageFile handles native Windows paths (including Cyrillic, spaces
            // and '#') and reports inaccessible/deleted files before playback.
            var file = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(_library[index].DocumentId));
            if (_disposed || version != _loadVersion) return;
            var source = MediaSource.CreateFromStorageFile(file);
            _player.Source = null;
            _source?.Dispose();
            _source = source;
            _player.Source = source;
            _loading = false;
            if (IsOn) TryPlay();
        }
        catch (Exception error)
        {
            if (_disposed || version != _loadVersion) return;
            AppLog.Error("music", "Opening an audio file failed", error);
            Fail($"Не удалось открыть аудиофайл. Проверь, что он доступен и его формат поддерживается Windows. {error.Message}");
        }
    }

    private void TryPlay()
    {
        try { _player.Play(); }
        catch (Exception error)
        {
            AppLog.Error("music", "Starting playback failed", error);
            Fail($"Не удалось запустить музыку: {error.Message}");
        }
    }

    private void ResetSource()
    {
        ++_loadVersion;
        _loading = false;
        CurrentIndex = null;
        _player.Pause();
        _player.Source = null;
        _source?.Dispose();
        _source = null;
    }

    private void Fail(string message)
    {
        IsOn = false;
        LastError = message;
        ResetSource();
    }
    public void MaybeAutostart() { if (Autostart) Play(); }
    // Used when an in-app video temporarily owns the audio. Window deactivation
    // itself does not call these on desktop, so music can play in the background.
    public void PauseForBackground() { _resume = IsOn; _player.Pause(); }
    public void ResumeFromBackground() { if (_resume) { _resume = false; Play(); } }
    public void Dispose()
    {
        _disposed = true;
        ResetSource();
        _player.Dispose();
    }
}
