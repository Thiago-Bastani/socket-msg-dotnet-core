/// <summary>Processa um pedido de "/grok" no SERVER, que é o único lado que chama a Grok API.</summary>
public static class GrokRequestHandler
{
    public static async Task<string> HandleAsync(string name, string question)
    {
        string answer;

        if (GrokConfig.GetApiKey() == null)
        {
            answer = "API do Grok não está configurada no server.";
        }
        else
        {
            try
            {
                answer = await GrokAgent.AskAsync(question);
            }
            catch (Exception ex)
            {
                answer = $"Erro: {ex.Message}";
            }
        }

        return $"[Grok] - @{name}, {answer}";
    }
}
