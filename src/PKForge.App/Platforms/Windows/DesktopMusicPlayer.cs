using System.Text.Json;
using PKForge.Domain;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace PKForge.App.Platforms.Windows;

public sealed class DesktopMusicPlayer : IMusicPlayer, IDisposable
{
    private readonly MediaPlayer _player = new();
    private readonly List<MusicTrack> _library;
    private bool _resume;
    public string? LastError { get; private set; }
    public IReadOnlyList<MusicTrack> Library => _library;
    public bool IsPlaying => _player.PlaybackSession.PlaybackState == MediaPlaybackState.Playing;
    public bool IsOn { get; private set; }
    public int? CurrentIndex { get; private set; }
    public MusicPlace Place { get; private set; }
    public MusicOrder Order => Enum.TryParse<MusicOrder>(Preferences.Default.Get("music_order", "InOrder"), out var value) ? value : MusicOrder.InOrder;
    public bool Autostart => Preferences.Default.Get("music_autostart", false);

    public DesktopMusicPlayer()
    {
        try { _library = JsonSerializer.Deserialize<List<MusicTrack>>(Preferences.Default.Get("desktop_music_library", "[]")) ?? []; }
        catch (JsonException) { _library = []; }
        _player.MediaEnded += (_, _) => MainThread.BeginInvokeOnMainThread(Skip);
        _player.MediaFailed += (_, args) => MainThread.BeginInvokeOnMainThread(() =>
        {
            LastError = args.ErrorMessage;
            IsOn = false;
        });
    }

    public int Add(IReadOnlyList<PickedDocument> documents)
    {
        var before = _library.Count;
        foreach (var document in documents)
            if (!_library.Any(t => t.DocumentId == document.DocumentId))
                _library.Add(new MusicTrack(document.DocumentId, document.DisplayName));
        Persist();
        return _library.Count - before;
    }

    public void Remove(int index)
    {
        Pause(); _library.RemoveAt(index); CurrentIndex = null; _player.Source = null; Persist();
    }
    public void Clear() { Pause(); _library.Clear(); CurrentIndex = null; _player.Source = null; Persist(); }
    private void Persist() => Preferences.Default.Set("desktop_music_library", JsonSerializer.Serialize(_library));
    public void SetOrder(MusicOrder order) => Preferences.Default.Set("music_order", order.ToString());
    public void SetAutostart(bool on) => Preferences.Default.Set("music_autostart", on);
    public MusicSource SourceFor(MusicPlace place) => MusicSource.Parse(Preferences.Default.Get($"music_source_{place}", ""));
    public void SetSource(MusicPlace place, MusicSource source)
    {
        Preferences.Default.Set($"music_source_{place}", source.Serialize());
        if (IsOn) { CurrentIndex = null; Play(); }
    }
    public void EnterPlace(MusicPlace place)
    {
        if (Place == place) return;
        Place = place;
        if (IsOn) { CurrentIndex = null; Play(); }
    }
    public void Play()
    {
        IsOn = true;
        var list = MusicPlaces.Playlist(Place, SourceFor, Library);
        if (list is null) { _player.Pause(); return; }
        if (CurrentIndex is not { } index || !list.Contains(index) || _player.Source is null)
        {
            CurrentIndex = list[0];
            _player.Source = MediaSource.CreateFromUri(new Uri(Path.GetFullPath(_library[list[0]].DocumentId)));
        }
        LastError = null;
        _player.Play();
    }
    public void Pause() { IsOn = false; _player.Pause(); }
    public void Skip()
    {
        var list = MusicPlaces.Playlist(Place, SourceFor, Library);
        if (list is null) return;
        var position = CurrentIndex is { } index ? Array.IndexOf(list, index) : -1;
        CurrentIndex = Order == MusicOrder.Shuffle ? list[Random.Shared.Next(list.Length)] : list[(position + 1) % list.Length];
        _player.Source = MediaSource.CreateFromUri(new Uri(Path.GetFullPath(_library[CurrentIndex.Value].DocumentId)));
        if (IsOn) _player.Play();
    }
    public void MaybeAutostart() { if (Autostart) Play(); }
    public void PauseForBackground() { _resume = IsOn; _player.Pause(); }
    public void ResumeFromBackground() { if (_resume) Play(); }
    public void Dispose() => _player.Dispose();
}
