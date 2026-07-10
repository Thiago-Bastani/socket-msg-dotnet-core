using System.Runtime.Versioning;
[assembly: SupportedOSPlatform("windows")]

Console.WriteLine("=== Socket Pool ===");
Console.Write("Seu nome: ");
string nome = Console.ReadLine()!;

Console.WriteLine("1 - Criar Pool (servidor)");
Console.WriteLine("2 - Entrar numa Pool (cliente)");
Console.Write("Escolha: ");
string? opcao = Console.ReadLine();

if (opcao == "1")
{
    Console.Write("Porta: ");
    int porta = int.Parse(Console.ReadLine()!);

    Console.Write("Chave da API do Grok (opcional, Enter para pular): ");
    string? grokKey = Console.ReadLine();
    if (!string.IsNullOrWhiteSpace(grokKey))
        Environment.SetEnvironmentVariable("GROK_API_KEY", grokKey.Trim());

    var server = new SocketServer(porta);
    server.MessageReceived += async msg =>
    {
        if (GrokRequestCodec.TryDecode(msg, out string reqName, out string question))
        {
            string questionMsg = $"[{reqName}] - /grok {question}";
            ConsoleUI.AddMessage(questionMsg);
            await server.BroadcastAsync(questionMsg);

            string grokMsg = await GrokRequestHandler.HandleAsync(reqName, question);
            ConsoleUI.AddMessage(grokMsg);
            await server.BroadcastAsync(grokMsg);
            return;
        }

        ConsoleUI.AddMessage(msg);
        await server.BroadcastAsync(msg);
    };
    ConsoleUI.Init();
    await server.RunAsync(nome);
}
else if (opcao == "2")
{
    Console.Write("IP: ");
    string ip = Console.ReadLine()!;

    Console.Write("Porta: ");
    int porta = int.Parse(Console.ReadLine()!);

    var client = new SocketClient();
    client.MessageReceived += msg => ConsoleUI.AddMessage(msg);
    ConsoleUI.Init();
    await client.ConnectAsync(ip, porta);
    await client.RunAsync(nome);
}
else
{
    Console.WriteLine("Opção inválida.");
}
