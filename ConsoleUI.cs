public static class ConsoleUI
{
    private static readonly List<string> _messages = new();
    private static readonly object _lock = new();
    private static string _inputBuffer = "";
    private const string Prompt = "> ";

    public static void Init()
    {
        Console.Clear();
        Redraw();
    }

    public static void AddMessage(string message)
    {
        lock (_lock)
        {
            int maxWidth = Math.Max(1, Console.WindowWidth - 1);
            while (message.Length > maxWidth)
            {
                _messages.Add(message[..maxWidth]);
                message = message[maxWidth..];
            }
            _messages.Add(message);
            Redraw();
        }
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
            string line = i < visible.Count ? visible[i] : "";
            if (line.Length > width) line = line[..width];
            Console.Write(line.PadRight(width));
        }

        Console.SetCursorPosition(0, msgHeight);
        Console.Write(new string('─', width));

        Console.SetCursorPosition(0, msgHeight + 1);
        string inputLine = Prompt + _inputBuffer;
        if (inputLine.Length > width) inputLine = inputLine[..width];
        Console.Write(inputLine.PadRight(width));

        int cursorX = Math.Min(Prompt.Length + _inputBuffer.Length, width);
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
                    Redraw();
                    return result;
                }
                else if (key.Key == ConsoleKey.Backspace)
                {
                    if (_inputBuffer.Length > 0)
                        _inputBuffer = _inputBuffer[..^1];
                }
                else if (!char.IsControl(key.KeyChar))
                {
                    _inputBuffer += key.KeyChar;
                }
                Redraw();
            }
        }
    }
}
