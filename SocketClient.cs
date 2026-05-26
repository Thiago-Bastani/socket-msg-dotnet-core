using System.Net.Sockets;
using System.Text;

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
        var buffer = new byte[4096];

        try
        {
            while (_running && _client.Connected)
            {
                int bytesRead = await stream.ReadAsync(buffer);
                if (bytesRead == 0) break;

                string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
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
        var data = Encoding.UTF8.GetBytes(message);
        await _client.GetStream().WriteAsync(data);
    }

    public async Task RunAsync(string name)
    {
        while (true)
        {
            string input = ConsoleUI.ReadInput();
            if (input == "sair") break;
            if (!string.IsNullOrEmpty(input))
            {
                string ascii = AsciiArt.TryConvert(input) is string art ? art : $"[{name}] - {input}";
                await SendAsync(ascii);
                ConsoleUI.AddMessage(ascii);
            }
        }

        Disconnect();
    }
}
