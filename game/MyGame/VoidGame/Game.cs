using System;
using System.Collections.Generic;
using ConsoleEngine;

namespace AlphabetDodge;

public enum GameState
{
    Playing,
    GameOver
}

public enum ObstacleKind
{
    NormalA,   // [A] 35%
    StunB,     // [B] 8%
    DarkC,     // [C] 7%
    SlowD,     // [D] 10%
    ConfuseE,  // [E] 8%
    FastF,     // [F] 15%
    RandomQ,   // [?] 5%
    InvincZ,   // [Z] 7% (Buff)
    HealX      // [X] 5% (Buff)
}

public class FallingObject
{
    public ObstacleKind Kind { get; set; }
    public string Label { get; set; } = string.Empty;
    public int X { get; set; }
    public float Y { get; set; }
    public float SpeedY { get; set; } // 칸/초
    public bool IsBuff { get; set; }
}

public class Game : GameBase
{
    private const int ScreenWidth = 80;
    private const int ScreenHeight = 40;
    private const int PlayerY = 38;
    private const int FloorY = 39;
    private const int SpawnStartY = 3;

    private readonly Random _rng = new();
    private readonly List<FallingObject> _objects = new();

    private GameState _state;
    private int _playerX;
    private int _hp;
    private float _elapsedTime;

    // 스폰 및 낙하 밸런스 변수
    private float _spawnInterval;
    private float _fallDuration;
    private float _spawnTimer;
    private int _spawnStepCount;
    private int _fallStepCount;

    // 상태이상 / 버프 잔여 시간 (초)
    private float _stunTimer;
    private float _darkTimer;
    private float _slowTimer;
    private float _confuseTimer;
    private float _invincibleTimer;

    public Game() : base(ScreenWidth, ScreenHeight, framesPerSecond: 20)
    {
        LoadAudioAssets();
        ResetGame();
    }

    private void LoadAudioAssets()
    {
        try
        {
            AudioManager.LoadSfx("hit", "sfx_jump.wav");
            AudioManager.LoadSfx("buff", "sfx_jump.wav");
            AudioManager.PlayBgm("bgm_loop.wav", loop: true);
        }
        catch
        {
            // 오디오 파일 또는 플랫폼 미지원 환경에서도 게임 진행에 영향이 없도록 무시
        }
    }

    private void ResetGame()
    {
        _state = GameState.Playing;
        _playerX = ScreenWidth / 2 - 1;
        _hp = 5;
        _elapsedTime = 0f;

        _spawnInterval = 2.0f;
        _fallDuration = 5.0f;
        _spawnTimer = 0f;
        _spawnStepCount = 0;
        _fallStepCount = 0;

        _stunTimer = 0f;
        _darkTimer = 0f;
        _slowTimer = 0f;
        _confuseTimer = 0f;
        _invincibleTimer = 0f;

        _objects.Clear();
    }

    protected override void Input(ConsoleKey key)
    {
        if (key == ConsoleKey.Escape)
        {
            AudioManager.StopAll();
            Quit();
            return;
        }

        if (_state == GameState.GameOver)
        {
            if (key == ConsoleKey.R)
            {
                ResetGame();
                try { AudioManager.PlayBgm("bgm_loop.wav", loop: true); } catch { }
            }
            return;
        }

        // 스턴 상태일 경우 조작 불가
        if (_stunTimer > 0f)
            return;

        int moveStep = _slowTimer > 0f ? 2 : 4;
        int direction = 0;

        if (key == ConsoleKey.LeftArrow)
            direction = -1;
        else if (key == ConsoleKey.RightArrow)
            direction = 1;

        if (direction != 0)
        {
            // 혼란 상태일 경우 좌/우 방향 반전
            if (_confuseTimer > 0f)
                direction = -direction;

            _playerX += direction * moveStep;

            // 플레이어 'ㅁ'(가로 2칸 폭)가 화면 경계(0 ~ 78)를 벗어나지 않도록 클램프
            if (_playerX < 0) _playerX = 0;
            if (_playerX > ScreenWidth - 2) _playerX = ScreenWidth - 2;
        }
    }

    protected override void Update(float deltaTime)
    {
        if (_state != GameState.Playing)
            return;

        _elapsedTime += deltaTime;

        // 디버프/버프 타이머 차감
        if (_stunTimer > 0f) _stunTimer = Math.Max(0f, _stunTimer - deltaTime);
        if (_darkTimer > 0f) _darkTimer = Math.Max(0f, _darkTimer - deltaTime);
        if (_slowTimer > 0f) _slowTimer = Math.Max(0f, _slowTimer - deltaTime);
        if (_confuseTimer > 0f) _confuseTimer = Math.Max(0f, _confuseTimer - deltaTime);
        if (_invincibleTimer > 0f) _invincibleTimer = Math.Max(0f, _invincibleTimer - deltaTime);

        // 난이도 상승: 생성 주기 감소 (매 7초마다 0.2초 감소, 최소 0.3초)
        int targetSpawnSteps = (int)(_elapsedTime / 7.0f);
        while (_spawnStepCount < targetSpawnSteps)
        {
            _spawnStepCount++;
            _spawnInterval = Math.Max(0.3f, _spawnInterval - 0.2f);
        }

        // 난이도 상승: 낙하 시간 감소 (매 8.5초마다 0.5초 감소, 최소 1.5초)
        int targetFallSteps = (int)(_elapsedTime / 8.5f);
        while (_fallStepCount < targetFallSteps)
        {
            _fallStepCount++;
            _fallDuration = Math.Max(1.5f, _fallDuration - 0.5f);
        }

        // 오브젝트 스폰
        _spawnTimer += deltaTime;
        if (_spawnTimer >= _spawnInterval)
        {
            _spawnTimer -= _spawnInterval;
            SpawnObject();
        }

        // 오브젝트 낙하 및 충돌 판정
        for (int i = _objects.Count - 1; i >= 0; i--)
        {
            var obj = _objects[i];
            obj.Y += obj.SpeedY * deltaTime;

            int objRow = (int)MathF.Floor(obj.Y);

            // 플레이어(Y=58, 폭 2칸: _playerX ~ _playerX+1)와 오브젝트([X], 폭 3칸: obj.X ~ obj.X+2) 충돌 판정
            if (objRow == PlayerY && CheckHorizontalOverlap(_playerX, 2, obj.X, 3))
            {
                ApplyObjectEffect(obj);
                _objects.RemoveAt(i);

                if (_hp <= 0)
                {
                    _hp = 0;
                    _state = GameState.GameOver;
                    AudioManager.StopBgm();
                    return;
                }
                continue;
            }

            // 바닥 도달 시 제거
            if (objRow >= FloorY)
            {
                _objects.RemoveAt(i);
            }
        }
    }

    private static bool CheckHorizontalOverlap(int ax, int aWidth, int bx, int bWidth)
    {
        return ax < bx + bWidth && ax + aWidth > bx;
    }

    private void SpawnObject()
    {
        // 전체 확률 100% 기준:
        // 디버프(88%): A(35), B(8), C(7), D(10), E(8), F(15), ?(5)
        // 버프(12%): Z(7), X(5)
        int roll = _rng.Next(100);
        ObstacleKind kind;
        string label;
        bool isBuff = false;

        if (roll < 35)
        {
            kind = ObstacleKind.NormalA;
            label = "[A]";
        }
        else if (roll < 43)
        {
            kind = ObstacleKind.StunB;
            label = "[B]";
        }
        else if (roll < 50)
        {
            kind = ObstacleKind.DarkC;
            label = "[C]";
        }
        else if (roll < 60)
        {
            kind = ObstacleKind.SlowD;
            label = "[D]";
        }
        else if (roll < 68)
        {
            kind = ObstacleKind.ConfuseE;
            label = "[E]";
        }
        else if (roll < 83)
        {
            kind = ObstacleKind.FastF;
            label = "[F]";
        }
        else if (roll < 88)
        {
            kind = ObstacleKind.RandomQ;
            label = "[?]";
        }
        else if (roll < 95)
        {
            kind = ObstacleKind.InvincZ;
            label = "[Z]";
            isBuff = true;
        }
        else
        {
            kind = ObstacleKind.HealX;
            label = "[X]";
            isBuff = true;
        }

        // 스폰 위치 Y=3 부터 바닥 Y=59 까지의 거리: 56칸
        float distance = FloorY - SpawnStartY;
        float speedY = distance / _fallDuration;

        // [F] 빠른 알파벳은 낙하 속도가 다른 알파벳 대비 50% 빠름
        if (kind == ObstacleKind.FastF)
        {
            speedY *= 1.5f;
        }

        // 오브젝트 가로 폭이 3칸("[A]")이므로 X는 0 ~ 77 범위
        int spawnX = _rng.Next(0, ScreenWidth - 2);

        _objects.Add(new FallingObject
        {
            Kind = kind,
            Label = label,
            X = spawnX,
            Y = SpawnStartY,
            SpeedY = speedY,
            IsBuff = isBuff
        });
    }

    private void ApplyObjectEffect(FallingObject obj)
    {
        if (obj.IsBuff)
        {
            try { AudioManager.PlaySfx("buff"); } catch { }

            if (obj.Kind == ObstacleKind.InvincZ)
            {
                _invincibleTimer = Math.Max(_invincibleTimer, 1.5f);
            }
            else if (obj.Kind == ObstacleKind.HealX)
            {
                if (_hp < 5)
                    _hp++;
            }
            return;
        }

        // 무적 상태일 경우 해로운 장애물 피격 무시
        if (_invincibleTimer > 0f)
            return;

        try { AudioManager.PlaySfx("hit"); } catch { }
        _hp--;

        switch (obj.Kind)
        {
            case ObstacleKind.NormalA:
            case ObstacleKind.FastF:
                break;

            case ObstacleKind.StunB:
                _stunTimer = Math.Max(_stunTimer, 0.5f);
                break;

            case ObstacleKind.DarkC:
                _darkTimer = Math.Max(_darkTimer, 0.3f);
                break;

            case ObstacleKind.SlowD:
                _slowTimer = Math.Max(_slowTimer, 0.5f);
                break;

            case ObstacleKind.ConfuseE:
                _confuseTimer = Math.Max(_confuseTimer, 1.0f);
                break;

            case ObstacleKind.RandomQ:
                ApplyRandomEffect();
                break;
        }
    }

    private void ApplyRandomEffect()
    {
        // 스턴(16%), 암흑(16%), 둔화(16%), 혼란(16%), 무적(18%), 회복(18%)
        int roll = _rng.Next(100);
        if (roll < 16)
        {
            _stunTimer = Math.Max(_stunTimer, 1.0f);
        }
        else if (roll < 32)
        {
            _darkTimer = Math.Max(_darkTimer, 0.5f);
        }
        else if (roll < 48)
        {
            _slowTimer = Math.Max(_slowTimer, 0.5f);
        }
        else if (roll < 64)
        {
            _confuseTimer = Math.Max(_confuseTimer, 1.0f);
        }
        else if (roll < 82)
        {
            _invincibleTimer = Math.Max(_invincibleTimer, 1.5f);
        }
        else
        {
            if (_hp < 5)
                _hp++;
        }
    }

    protected override void Render(Screen screen)
    {
        if (_state == GameState.GameOver)
        {
            RenderGameOver(screen);
            return;
        }

        // 암흑([C]) 디버프 발동 시 게임 화면 전체를 숨김 (0.3초)
        if (_darkTimer > 0f)
        {
            return;
        }

        // 1. 떨어지는 오브젝트 먼저 렌더링
        foreach (var obj in _objects)
        {
            int drawY = (int)MathF.Floor(obj.Y);
            if (drawY >= 0 && drawY < FloorY)
            {
                screen.DrawText(obj.X, drawY, obj.Label);
            }
        }

        // 2. 시간 경과에 따른 중앙 가로 시야 차단 층 덮어그리기 (장애물 뒤에 그려 가림 처리)
        RenderBlindLayer(screen);

        // 3. 플레이어 캐릭터 렌더링 (무적 상태일 때는 깜빡거림)
        bool showPlayer = true;
        if (_invincibleTimer > 0f)
        {
            // 0.1초 주기로 깜빡임
            showPlayer = ((int)(_invincibleTimer * 20)) % 2 == 0;
        }

        if (showPlayer)
        {
            // Screen.DrawText 주석(한글 2칸 차지)을 고려하여 1칸씩 기록 후 공백 처리하거나 DrawText 사용
            screen.Draw(_playerX, PlayerY, 'ㅁ');
        }

        // 4. 바닥선 렌더링 (Y=59)
        for (int x = 0; x < ScreenWidth; x++)
        {
            screen.Draw(x, FloorY, '=');
        }

        // 5. 상단 HUD 렌더링 (우측 상단)
        RenderHud(screen);
    }

    private void RenderBlindLayer(Screen screen)
    {
        // 0 ~ 29초: 없음
        // 30초 경과 시: 1줄 (Y=30)
        // 이후 10초 추가 경과 시마다 1줄씩 위쪽으로 확장, 최대 5줄 제한
        if (_elapsedTime < 30f)
            return;

        int extraLines = (int)((_elapsedTime - 30f) / 10f);
        int totalLines = Math.Min(5, 1 + extraLines);

        for (int i = 0; i < totalLines; i++)
        {
            int y = 30 - i;
            if (y < 0 || y >= ScreenHeight) continue;

            for (int x = 0; x < ScreenWidth; x++)
            {
                screen.Draw(x, y, '▒');
            }
        }
    }

    private void RenderHud(Screen screen)
    {
        // 생명 표시: 하트 모양('♥')으로 남은 개수만큼 표시 (예: "♥ ♥ ♥ ♥ ♥")
        string hearts = string.Empty;
        for (int i = 0; i < _hp; i++)
        {
            hearts += (i > 0 ? " ♥" : "♥");
        }

        // 우측 상단 정렬 (X=68 기준)
        screen.DrawText(68, 1, hearts);

        // 생존 시간: 시작 시 "00초", 60초 초과 시 "1:01" 형태
        int totalSeconds = (int)_elapsedTime;
        string timeText;
        if (totalSeconds <= 60)
        {
            timeText = $"{totalSeconds:D2}초";
        }
        else
        {
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            timeText = $"{minutes}:{seconds:D2}";
        }

        // 우측 상단 생명 표시 바로 아래에 배치
        int timeX = ScreenWidth - 4 - timeText.Length;
        screen.DrawText(timeX, 2, timeText);
    }

    private void RenderGameOver(Screen screen)
    {
        string[] banner =
        {
            "  ████    ███   █   █  █████       ███   █   █  █████  ████  ",
            " █       █   █  ██ ██  █          █   █  █   █  █      █   █ ",
            " █  ███  █████  █ █ █  ████       █   █  █   █  ████   ████  ",
            " █    █  █   █  █   █  █          █   █   █ █   █      █   █ ",
            "  ████   █   █  █   █  █████       ███     █    █████  █   █ "
        };

        int bannerStartY = 14;
        for (int i = 0; i < banner.Length; i++)
        {
            int x = (ScreenWidth - banner[i].Length) / 2;
            screen.DrawText(x, bannerStartY + i, banner[i]);
        }

        // 최종 생존 시간 요약 표시
        int totalSeconds = (int)_elapsedTime;
        string recordText = totalSeconds <= 60
            ? $"SURVIVAL TIME : {totalSeconds:D2}s"
            : $"SURVIVAL TIME : {totalSeconds / 60}:{totalSeconds % 60:D2}";
        screen.DrawText((ScreenWidth - recordText.Length) / 2, 23, recordText);

        // 재시작 안내 박스
        string boxTop = "╔══════════════════════════════════════════════════╗";
        string boxEmpty = "║                                                  ║";
        string boxPrompt = "║ p r e s s  ' R '   다 시  시 작 하 시 겠 습 니 까║";
        string boxBottom = "╚══════════════════════════════════════════════════╝";

        int boxX = (ScreenWidth - boxTop.Length) / 2;
        screen.DrawText(boxX, 27, boxTop);
        screen.DrawText(boxX, 28, boxEmpty);
        screen.DrawText(boxX, 29, boxPrompt);
        screen.DrawText(boxX, 30, boxEmpty);
        screen.DrawText(boxX, 31, boxBottom);
    }
}
