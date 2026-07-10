using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Net.NetworkInformation;
using System.Runtime.Versioning;

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

        try
        {
            while (client.Connected)
            {
                string? message = await SocketFraming.ReadAsync(stream);
                if (message == null) break;
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

    public async Task BroadcastAsync(string message)
    {
        var dead = new List<TcpClient>();

        foreach (var client in _clients.ToList())
        {
            try
            {
                await SocketFraming.WriteAsync(client.GetStream(), message);
            }
            catch
            {
                dead.Add(client);
            }
        }

        foreach (var client in dead)
            _clients.Remove(client);
    }

    [SupportedOSPlatform("windows")]
    public async Task RunAsync(string name)
    {
        Start();
        while (true)
        {
            string input = ConsoleUI.ReadInput();
            if (input == "sair") break;
            if (!string.IsNullOrEmpty(input))
            {
                if (input.StartsWith("/grok", StringComparison.OrdinalIgnoreCase))
                {
                    string question = input.Length > 5 ? input[5..].Trim() : "";
                    if (string.IsNullOrEmpty(question))
                    {
                        ConsoleUI.AddMessage("[Grok] Uso: /grok <pergunta>");
                    }
                    else
                    {
                        ConsoleUI.AddMessage("[Grok] Pensando...");
                        string grokMsg = await GrokRequestHandler.HandleAsync(name, question);
                        await BroadcastAsync(grokMsg);
                        ConsoleUI.AddMessage(grokMsg);
                    }
                    continue;
                }

                string? path = AsciiArt.ExtractPath(input);
                if (path != null && File.Exists(path) && AsciiArt.IsGif(path))
                {
                    try
                    {
                        var gif = AsciiArt.ConvertGif(path);
                        string gifMsg = GifMessageCodec.Encode($"----- [{name}] -----", gif);
                        await BroadcastAsync(gifMsg);
                        ConsoleUI.AddMessage(gifMsg);
                    }
                    catch (Exception ex)
                    {
                        ConsoleUI.AddMessage($"[AsciiArt] Erro: {ex.Message}");
                    }
                    continue;
                }

                string msg = AsciiArt.TryConvert(input) is string art
                    ? $"----- [{name}] -----\n{art}"
                    : ConsoleUI.ApplyPersistentColor($"[{name}] - {input}");
                await BroadcastAsync(msg);
                ConsoleUI.AddMessage(msg);
            }
        }

        Stop();
    }
}
