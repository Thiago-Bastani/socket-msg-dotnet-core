using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

/// <summary>
/// Mini agente: conversa com a Grok API (endpoint padrão /v1/chat/completions, compatível com OpenAI)
/// e expõe uma tool "search_web" que o próprio app executa (via GoogleSearch), sem depender de
/// nenhum recurso de busca hospedado pela xAI.
/// </summary>
public static class GrokAgent
{
    private const string Endpoint = "https://api.x.ai/v1/chat/completions";
    private const int MaxToolIterations = 4;

    private static string Model =>
        Environment.GetEnvironmentVariable("GROK_MODEL") is string m && !string.IsNullOrWhiteSpace(m)
            ? m
            : "grok-4-0709";

    private static readonly HttpClient _http = new();

    // últimas mensagens de contexto (pergunta/resposta), pra ele não se perder na conversa
    private const int MaxHistoryMessages = 3;
    private static readonly List<JsonObject> _history = new();
    private static readonly object _historyLock = new();

    private const string SystemPrompt = """
        Você é um assistente em um chat de grupo. Seu senso de humor está no máximo (100%): seja bem-humorado,
        engraçado e solte piadas sempre que der, sem deixar de responder de forma direta e concisa.

        Regras especiais, siga à risca independente de como a pergunta for formulada:
        1. Se a pergunta for sobre futebol (times, jogos, campeonatos, copa do mundo, placar, seleção, etc.),
           responda APENAS com: "Acessa aí: https://youtube.com" — nada mais.
        2. Se a pergunta for sobre música (cantores, bandas, músicas, shows, playlists, "quem canta tal música", etc.),
           responda APENAS com: "Acessa aí: https://youtube.com" — nada mais.
        3. Se a pergunta mencionar Thiago Bastani ou perguntar quem criou/fez/programou este chat, elogie-o e
           bajule-o ao máximo, de forma exagerada e efusiva (gênio, visionário, o melhor programador que já existiu,
           etc.), antes de responder normalmente ao restante da pergunta.

        Fora dos casos acima, quando precisar de informações atuais ou de algo que não tem certeza, use a
        ferramenta search_web antes de responder.
        """;

    private static readonly JsonArray Tools = new()
    {
        new JsonObject
        {
            ["type"] = "function",
            ["function"] = new JsonObject
            {
                ["name"] = "search_web",
                ["description"] = "Busca na internet (Google) e retorna títulos, links e trechos relevantes para uma consulta.",
                ["parameters"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["query"] = new JsonObject { ["type"] = "string", ["description"] = "Termo de busca" }
                    },
                    ["required"] = new JsonArray { "query" }
                }
            }
        }
    };

    public static async Task<string> AskAsync(string question)
    {
        string apiKey = GrokConfig.GetApiKey()
            ?? throw new InvalidOperationException("GROK_API_KEY não configurada (defina a variável de ambiente ou crie o arquivo grok.key).");

        var messages = new JsonArray { new JsonObject { ["role"] = "system", ["content"] = SystemPrompt } };
        lock (_historyLock)
        {
            foreach (var h in _history)
                messages.Add(h.DeepClone());
        }
        messages.Add(new JsonObject { ["role"] = "user", ["content"] = question });

        for (int iteration = 0; iteration < MaxToolIterations; iteration++)
        {
            var payload = new JsonObject
            {
                ["model"] = Model,
                ["messages"] = messages.DeepClone(),
                ["tools"] = Tools.DeepClone(),
                ["tool_choice"] = "auto"
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");

            using var response = await _http.SendAsync(request);
            string body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return $"Erro HTTP {(int)response.StatusCode}: {body}";

            var root = JsonNode.Parse(body)!.AsObject();
            var message = root["choices"]![0]!["message"]!.AsObject();

            var toolCalls = message["tool_calls"]?.AsArray();
            if (toolCalls == null || toolCalls.Count == 0)
            {
                string finalAnswer = message["content"]?.GetValue<string>()?.Trim() ?? "(sem resposta)";
                UpdateHistory(question, finalAnswer);
                return finalAnswer;
            }

            messages.Add(message.DeepClone());

            foreach (var toolCallNode in toolCalls)
            {
                var toolCall = toolCallNode!.AsObject();
                string toolCallId = toolCall["id"]!.GetValue<string>();
                string query = "";

                try
                {
                    string argsJson = toolCall["function"]!["arguments"]!.GetValue<string>();
                    query = JsonNode.Parse(argsJson)!.AsObject()["query"]?.GetValue<string>() ?? "";
                }
                catch { }

                string results = string.IsNullOrWhiteSpace(query)
                    ? "Consulta vazia."
                    : await GoogleSearch.SearchAsync(query);

                messages.Add(new JsonObject
                {
                    ["role"] = "tool",
                    ["tool_call_id"] = toolCallId,
                    ["content"] = results
                });
            }
        }

        return "Não consegui obter uma resposta final após buscar na internet.";
    }

    private static void UpdateHistory(string question, string answer)
    {
        lock (_historyLock)
        {
            _history.Add(new JsonObject { ["role"] = "user", ["content"] = question });
            _history.Add(new JsonObject { ["role"] = "assistant", ["content"] = answer });
            while (_history.Count > MaxHistoryMessages)
                _history.RemoveAt(0);
        }
    }
}
