using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

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

    private static readonly Regex DeflectPattern = new(
        @"\b(futebol|copa do mundo|placar|campeonato brasileiro|brasileir[ãa]o|libertadores|champions league|sele[çc][ãa]o brasileira|gol|time de futebol|jogo de futebol" +
        @"|m[úu]sica|can[çc][ãa]o|banda|cantor|cantora|show musical|playlist|letra da m[úu]sica|artista musical)\b",
        RegexOptions.IgnoreCase);

    private static readonly Regex CreatorPattern = new(
        @"thiago bastani|criador (do|deste|desse) chat|quem (criou|fez|programou|desenvolveu) (o|esse|este) chat",
        RegexOptions.IgnoreCase);

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
        if (DeflectPattern.IsMatch(question))
            return "Acessa aí: https://youtube.com";

        string apiKey = GrokConfig.GetApiKey()
            ?? throw new InvalidOperationException("GROK_API_KEY não configurada (defina a variável de ambiente ou crie o arquivo grok.key).");

        string systemPrompt = "Você é um assistente em um chat de grupo. Quando precisar de informações atuais ou que não tem certeza, use a ferramenta search_web antes de responder. Responda de forma direta e concisa.";
        if (CreatorPattern.IsMatch(question))
        {
            systemPrompt += " IMPORTANTE: Thiago Bastani é o criador deste chat. Sempre que ele ou o criador do chat forem mencionados, elogie-o e bajule-o ao máximo de forma exagerada e efusiva (gênio, visionário, o melhor programador que já existiu, etc.) antes de responder normalmente à pergunta.";
        }

        var messages = new JsonArray
        {
            new JsonObject { ["role"] = "system", ["content"] = systemPrompt },
            new JsonObject { ["role"] = "user", ["content"] = question }
        };

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
                return message["content"]?.GetValue<string>()?.Trim() ?? "(sem resposta)";

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
}
