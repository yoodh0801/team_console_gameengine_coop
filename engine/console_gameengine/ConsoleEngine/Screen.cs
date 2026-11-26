using System.Text;

namespace ConsoleEngine;

/// <summary>한 프레임을 그려 두는 문자 버퍼. Present 때 한 번에 출력한다.</summary>
using System;
using System.Text;

public class Screen
{
    private readonly struct Cell
    {
        public readonly char Char;
        public readonly ConsoleColor? Foreground;
        public readonly ConsoleColor? Background;

        public Cell(char c, ConsoleColor? fg, ConsoleColor? bg)
        {
            Char = c;
            Foreground = fg;
            Background = bg;
        }
    }

    public readonly int Width;
    public readonly int Height;
    private readonly Cell[,] _cells;
    private readonly StringBuilder _frame = new StringBuilder();

    public Screen(int width, int height, ConsoleColor? defaultFg = null, ConsoleColor? defaultBg = null)
    {
        Width = width;
        Height = height;
        _cells = new Cell[height, width];
        Clear(defaultFg, defaultBg);
        TryResizeConsole(Width, Height);
    }

    private static void TryResizeConsole(int width, int height)
    {
        if (!OperatingSystem.IsWindows()) return;

        try
        {
            int maxW = Math.Max(1, Console.LargestWindowWidth);
            int maxH = Math.Max(1, Console.LargestWindowHeight);
            int targetW = Math.Clamp(width, 1, maxW);
            int targetH = Math.Clamp(height, 1, maxH);

            if (targetW < Console.BufferWidth)
                Console.SetWindowSize(targetW, Console.WindowHeight);
            if (targetH < Console.BufferHeight)
                Console.SetBufferSize(Console.BufferWidth, targetH);

            Console.SetBufferSize(targetW, targetH);
            Console.SetWindowSize(targetW, targetH);
        }
        catch (Exception)
        {
            // 리다이렉트된 출력, 비표준 터미널, 권한/크기 제한 등 환경 예외 무시
        }
    }

    public void Clear(ConsoleColor? fg = null, ConsoleColor? bg = null)
    {
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                _cells[y, x] = new Cell(' ', fg, bg);
    }

    public void Draw(int x, int y, char c, ConsoleColor? fg = null, ConsoleColor? bg = null)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) return; // 화면 밖은 무시
        _cells[y, x] = new Cell(c, fg, bg);
    }

    /// <summary>(x, y)부터 한 글자씩 오른쪽으로 그린다. 화면 밖 글자는 무시. (v0.2.0, #6)</summary>
    /// <param name="text">ASCII 문자만. 한글은 2칸을 차지해 줄이 밀린다 (ADR 0002)</param>
    public void DrawText(int x, int y, string text, ConsoleColor? fg = null, ConsoleColor? bg = null)
    {
        for (int i = 0; i < text.Length; i++)
            Draw(x + i, y, text[i], fg, bg);
    }

    internal void Present()
    {
        ConsoleColor prevFg = Console.ForegroundColor;
        ConsoleColor prevBg = Console.BackgroundColor;
        ConsoleColor currentFg = prevFg;
        ConsoleColor currentBg = prevBg;

        Console.SetCursorPosition(0, 0); // 지우지 않고 덮어써서 깜빡임 방지

        for (int y = 0; y < Height; y++)
        {
            _frame.Clear();

            for (int x = 0; x < Width; x++)
            {
                Cell cell = _cells[y, x];
                ConsoleColor targetFg = cell.Foreground ?? prevFg;
                ConsoleColor targetBg = cell.Background ?? prevBg;

                if (targetFg != currentFg || targetBg != currentBg)
                {
                    if (_frame.Length > 0)
                    {
                        Console.Write(_frame.ToString());
                        _frame.Clear();
                    }

                    if (targetFg != currentFg)
                    {
                        Console.ForegroundColor = targetFg;
                        currentFg = targetFg;
                    }

                    if (targetBg != currentBg)
                    {
                        Console.BackgroundColor = targetBg;
                        currentBg = targetBg;
                    }
                }

                _frame.Append(cell.Char);
            }

            if (_frame.Length > 0)
            {
                Console.Write(_frame.ToString());
            }

            if (currentFg != prevFg || currentBg != prevBg)
            {
                Console.ForegroundColor = prevFg;
                Console.BackgroundColor = prevBg;
                currentFg = prevFg;
                currentBg = prevBg;
            }

            Console.Write('\n');
        }

        Console.ForegroundColor = prevFg;
        Console.BackgroundColor = prevBg;
    }
}