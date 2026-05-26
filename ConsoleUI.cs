using System.Text.RegularExpressions;

public static class ConsoleUI
{
    private record Message(string Text, string? AnsiColor);

    private static readonly List<Message> _messages = new();
    private static readonly object _lock = new();
    private static string _inputBuffer = "";
    private static int _cursorPos = 0; // posição do cursor dentro do buffer
    private const string Prompt = "> ";
    private const string Reset = "\x1b[0m";

    // detecta {#rrggbb} no final da mensagem
    private static readonly Regex ColorTag = new(@"\{#([0-9a-fA-F]{6})\}\s*$");

    public static void Init()
    {
        Console.Clear();
        Redraw();
    }

    public static void AddMessage(string raw)
    {
        lock (_lock)
        {
            // suporte a mensagens com múltiplas linhas (ex: ascii art)
            foreach (var line in raw.Split('\n'))
                AddLine(line);
            Redraw();
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
        var visible = _messages.TakeLast(msgHeight).ToList();

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

        Console.SetCursorPosition(0, msgHeight);
        Console.Write(new string('─', width));

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
                    Redraw();
                    return result;
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
