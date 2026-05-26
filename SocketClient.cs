using System.Net.Sockets;
using System.Text;
using System.Runtime.Versioning;

public class SocketClient
{
    private TcpClient _client = new();
    private bool _running;

    public event Action<string>? MessageReceived;

    public async Task ConnectAsync(string ip, int port)
    {
        await _client.ConnectAsync(ip, port);
        _running = true;
        ConsoleUI.AddMessage($"[Client] Connected to {ip}:{port}");
        _ = Task.Run(ReadLoop);
    }

    public void Disconnect()
    {
        _running = false;
        _client.Dispose();
    }

    private async Task ReadLoop()
    {
        var stream = _client.GetStream();

        try
        {
            while (_running && _client.Connected)
            {
                string? message = await SocketFraming.ReadAsync(stream);
                if (message == null) break;
                MessageReceived?.Invoke(message);
            }
        }
        catch { }
        finally
        {
            ConsoleUI.AddMessage("[Client] Disconnected from server.");
        }
    }

    public async Task SendAsync(string message)
    {
        if (!_client.Connected) return;
        await SocketFraming.WriteAsync(_client.GetStream(), message);
    }

    [SupportedOSPlatform("windows")]
    public async Task RunAsync(string name)
    {
        while (true)
        {
            string input = ConsoleUI.ReadInput();
            if (input == "sair") break;
            if (!string.IsNullOrEmpty(input))
            {
                string ascii = AsciiArt.TryConvert(input) is string art
                    ? $"----- [{name}] -----\n{art}"
                    : $"[{name}] - {input}";
                await SendAsync(ascii);
                ConsoleUI.AddMessage(ascii);
            }
        }

        Disconnect();
    }
}
