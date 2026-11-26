using ConsoleEngine;
using System.Media;

public class Audio : IDisposable
{
    private readonly Dictionary<string, SoundPlayer> _sfxPlayers = new();
    private readonly object _lock = new();

    private SoundPlayer? _bgmPlayer;
    private bool _isBgmLooping;
    private bool _disposed;

    /// <summary>
    /// 효과음(.wav)을 미리 로드합니다.
    /// </summary>
    public void LoadSfx(string key, string filePath)
    {
        ThrowIfDisposed();
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"효과음 파일을 찾을 수 없습니다: {filePath}", filePath);

        var player = new SoundPlayer(filePath);
        player.Load();

        lock (_lock)
        {
            if (_sfxPlayers.TryGetValue(key, out var oldPlayer))
            {
                oldPlayer.Stop();
                oldPlayer.Dispose();
            }

            _sfxPlayers[key] = player;
        }
    }

    /// <summary>
    /// 효과음을 비동기로 재생합니다.
    /// 같은 키를 빠르게 연타하면 이전 재생이 끊길 수 있습니다.
    /// </summary>
    public void PlaySfx(string key)
    {
        ThrowIfDisposed();

        SoundPlayer? player;
        lock (_lock)
        {
            if (!_sfxPlayers.TryGetValue(key, out player))
                return;
        }

        player.Play();
    }

    /// <summary>
    /// 배경음악(.wav)을 재생합니다.
    /// </summary>
    public void PlayBgm(string filePath, bool loop = true)
    {
        ThrowIfDisposed();
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"BGM 파일을 찾을 수 없습니다: {filePath}", filePath);

        lock (_lock)
        {
            StopBgmInternal();

            _bgmPlayer = new SoundPlayer(filePath);
            _bgmPlayer.Load();
            _isBgmLooping = loop;

            if (loop)
                _bgmPlayer.PlayLooping();
            else
                _bgmPlayer.Play();
        }
    }

    public void StopBgm()
    {
        lock (_lock)
        {
            StopBgmInternal();
        }
    }

    /// <summary>
    /// 효과음과 BGM을 모두 멈춥니다.
    /// </summary>
    public void StopAll()
    {
        lock (_lock)
        {
            StopBgmInternal();
            foreach (var player in _sfxPlayers.Values)
            {
                player.Stop();
            }
        }
    }

    private void StopBgmInternal()
    {
        if (_bgmPlayer != null)
        {
            _bgmPlayer.Stop();
            _bgmPlayer.Dispose();
            _bgmPlayer = null;
            _isBgmLooping = false;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(Audio));
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        lock (_lock)
        {
            _disposed = true;
            StopBgmInternal();

            foreach (var player in _sfxPlayers.Values)
            {
                player.Stop();
                player.Dispose();
            }

            _sfxPlayers.Clear();
        }
    }
}
