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

    var server = new SocketServer(porta);
    server.MessageReceived += msg => ConsoleUI.AddMessage(msg);
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
