using ConsoleEngine;

public static class AudioManager
{
    private static readonly object _syncRoot = new();
    private static Audio? _audio;

    public static void Register(Audio audio)
    {
        _audio = audio;
    }

    public static void LoadSfx(string key, string filePath)
    {
        lock (_syncRoot)
        {
            EnsureInitialized();
            _audio?.LoadSfx(key, filePath);
        }
    }

    public static void PlaySfx(string key)
    {
        lock (_syncRoot)
        {
            EnsureInitialized();
            _audio?.PlaySfx(key);
        }
    }

    public static void PlayBgm(string filePath, bool loop = true)
    {
        lock (_syncRoot)
        {
            EnsureInitialized();
            _audio?.PlayBgm(filePath, loop);
        }
    }

    public static void StopBgm()
    {
        lock (_syncRoot)
        {
            EnsureInitialized();
            _audio?.StopBgm();
        }
    }

    public static void StopAll()
    {
        lock (_syncRoot)
        {
            EnsureInitialized();
            _audio?.StopAll();
        }
    }

    /// <summary>
    /// 프로그램 종료 시 오디오 리소스를 정리합니다.
    /// 이후 다시 재생하려면 새 Audio 인스턴스로 재초기화합니다.
    /// </summary>
    public static void Shutdown()
    {
        lock (_syncRoot)
        {
            _audio?.Dispose();
            _audio = new Audio();
        }
    }

    private static void EnsureInitialized()
    {
        // Shutdown 후 재호출되더라도 안전하게 동작하도록 유지합니다.
        _audio ??= new Audio();
    }
}
