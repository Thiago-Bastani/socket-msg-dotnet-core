using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Net.NetworkInformation;

public class SocketServer
{
    private TcpListener _listener;
    private readonly List<TcpClient> _clients = new();
    private bool _running;

    public event Action<string>? MessageReceived;

    public SocketServer(int port)
    {
        _listener = new TcpListener(IPAddress.Any, port);
    }

    public void Start()
    {
        _listener.Start();
        _running = true;

        var localIp = Dns.GetHostAddresses(Dns.GetHostName())
            .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
            ?.ToString() ?? "desconhecido";

        ConsoleUI.AddMessage($"[Server] Pool criada! IP: {localIp} | Porta: {((IPEndPoint)_listener.LocalEndpoint).Port}");
        Task.Run(AcceptLoop);
    }

    public void Stop()
    {
        _running = false;
        _listener.Stop();
    }

    private async Task AcceptLoop()
    {
        while (_running)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync();
                _clients.Add(client);
                ConsoleUI.AddMessage("[Server] Client connected.");
                _ = Task.Run(() => ReadLoop(client));
            }
            catch { break; }
        }
    }

    private async Task ReadLoop(TcpClient client)
    {
        var stream = client.GetStream();
        var buffer = new byte[4096];

        try
        {
            while (client.Connected)
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
            _clients.Remove(client);
            client.Dispose();
            ConsoleUI.AddMessage("[Server] Client disconnected.");
        }
    }

    public async Task SendAsync(string message)
    {
        var data = Encoding.UTF8.GetBytes(message);
        var dead = new List<TcpClient>();

        foreach (var client in _clients.ToList())
        {
            try
            {
                await client.GetStream().WriteAsync(data);
            }
            catch
            {
                dead.Add(client);
            }
        }

        foreach (var client in dead)
            _clients.Remove(client);
    }

    public async Task RunAsync(string name)
    {
        Start();
        while (true)
        {
            string input = ConsoleUI.ReadInput();
            if (input == "sair") break;
            if (!string.IsNullOrEmpty(input))
            {
                await SendAsync($"[{name}] - {input}");
                ConsoleUI.AddMessage($"[{name}] - {input}");
            }
        }

        Stop();
    }
}
