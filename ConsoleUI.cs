using System.Text.RegularExpressions;

public static class ConsoleUI
{
    private record Message(string Text, string? AnsiColor);

    private static readonly List<Message> _messages = new();
    private static readonly object _lock = new();
    private static string _inputBuffer = "";
    private static int _cursorPos = 0;
    private static int _scrollOffset = 0; // 0 = fim (mais recente), positivo = scrollado pra cima
    private const string Prompt = "> ";
    private const string Reset = "\x1b[0m";

    // detecta {#rrggbb} no final da mensagem
    private static readonly Regex ColorTag = new(@"\{#([0-9a-fA-F]{6})\}\s*$");

    public static void Init()
    {
        Console.Clear();
        Redraw();
        Task.Run(ResizeWatchLoop);
    }

    private static async Task ResizeWatchLoop()
    {
        int lastWidth = Console.WindowWidth;
        int lastHeight = Console.WindowHeight;

        while (true)
        {
            await Task.Delay(500);
            int w = Console.WindowWidth;
            int h = Console.WindowHeight;
            if (w != lastWidth || h != lastHeight)
            {
                lastWidth = w;
                lastHeight = h;
                lock (_lock) { Console.Clear(); Redraw(); }
            }
        }
    }

    public static void AddMessage(string raw)
    {
        lock (_lock)
        {
            foreach (var line in raw.Split('\n'))
                AddLine(line);
            // só auto-scroll se já estava no fim
            if (_scrollOffset == 0)
                Redraw();
            else
                Redraw(); // redesenha mas mantém posição
        }
    }

    private static void AddLine(string raw)
    {
        string? ansi = null;
        var match = ColorTag.Match(raw);
        if (match.Success)
        {
            string hex = match.Groups[1].Value;
            int r = Convert.ToInt32(hex[..2], 16);
            int g = Convert.ToInt32(hex[2..4], 16);
            int b = Convert.ToInt32(hex[4..6], 16);
            ansi = $"\x1b[38;2;{r};{g};{b}m";
            raw = raw[..match.Index].TrimEnd();
        }

        int maxWidth = Math.Max(1, Console.WindowWidth - 1);
        while (raw.Length > maxWidth)
        {
            _messages.Add(new(raw[..maxWidth], ansi));
            raw = raw[maxWidth..];
        }
        _messages.Add(new(raw, ansi));
    }

    private static int MessageAreaHeight => Math.Max(1, Console.WindowHeight - 2);

    private static void Redraw()
    {
        int msgHeight = MessageAreaHeight;
        int width = Math.Max(1, Console.WindowWidth - 1);

        // clamp scroll offset
        int maxScroll = Math.Max(0, _messages.Count - msgHeight);
        _scrollOffset = Math.Clamp(_scrollOffset, 0, maxScroll);

        int startIdx = _messages.Count - msgHeight - _scrollOffset;
        startIdx = Math.Max(0, startIdx);
        var visible = _messages.Skip(startIdx).Take(msgHeight).ToList();

        Console.CursorVisible = false;

        for (int i = 0; i < msgHeight; i++)
        {
            Console.SetCursorPosition(0, i);
            if (i < visible.Count)
            {
                string line = visible[i].Text;
                if (line.Length > width) line = line[..width];
                if (visible[i].AnsiColor is string color)
                {
                    Console.Write(color);
                    Console.Write(line);
                    Console.Write(Reset);
                    Console.Write(new string(' ', width - line.Length));
                }
                else
                {
                    Console.Write(line.PadRight(width));
                }
            }
            else
            {
                Console.Write(new string(' ', width));
            }
        }

        // separador com indicador de scroll
        Console.SetCursorPosition(0, msgHeight);
        string scrollInfo = _scrollOffset > 0 ? $" [↑ +{_scrollOffset} linhas] " : "";
        string sep = new string('─', Math.Max(0, width - scrollInfo.Length));
        Console.Write(sep + scrollInfo);

        Console.SetCursorPosition(0, msgHeight + 1);
        string inputLine = Prompt + _inputBuffer;
        if (inputLine.Length > width) inputLine = inputLine[..width];
        Console.Write(inputLine.PadRight(width));

        int cursorX = Math.Min(Prompt.Length + _cursorPos, width);
        Console.SetCursorPosition(cursorX, msgHeight + 1);
        Console.CursorVisible = true;
    }

    public static string ReadInput()
    {
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            lock (_lock)
            {
                if (key.Key == ConsoleKey.Enter)
                {
                    string result = _inputBuffer;
                    _inputBuffer = "";
                    _cursorPos = 0;
                    _scrollOffset = 0; // volta ao fim ao enviar
                    Redraw();
                    return result;
                }
                else if (key.Key == ConsoleKey.PageUp)
                {
                    _scrollOffset += MessageAreaHeight / 2;
                }
                else if (key.Key == ConsoleKey.PageDown)
                {
                    _scrollOffset = Math.Max(0, _scrollOffset - MessageAreaHeight / 2);
                }
                else if (key.Key == ConsoleKey.LeftArrow)
                {
                    if (_cursorPos > 0) _cursorPos--;
                }
                else if (key.Key == ConsoleKey.RightArrow)
                {
                    if (_cursorPos < _inputBuffer.Length) _cursorPos++;
                }
                else if (key.Key == ConsoleKey.Home)
                {
                    _cursorPos = 0;
                }
                else if (key.Key == ConsoleKey.End)
                {
                    _cursorPos = _inputBuffer.Length;
                }
                else if (key.Key == ConsoleKey.Backspace)
                {
                    if (_cursorPos > 0)
                    {
                        _inputBuffer = _inputBuffer[..(_cursorPos - 1)] + _inputBuffer[_cursorPos..];
                        _cursorPos--;
                    }
                }
                else if (key.Key == ConsoleKey.Delete)
                {
                    if (_cursorPos < _inputBuffer.Length)
                        _inputBuffer = _inputBuffer[.._cursorPos] + _inputBuffer[(_cursorPos + 1)..];
                }
                else if (!char.IsControl(key.KeyChar))
                {
                    _inputBuffer = _inputBuffer[.._cursorPos] + key.KeyChar + _inputBuffer[_cursorPos..];
                    _cursorPos++;
                }
                Redraw();
            }
        }
    }
}
