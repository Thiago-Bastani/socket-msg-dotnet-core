using System.Net;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Busca na internet feita pelo próprio app: dá um GET direto em www.google.com/search e
/// extrai título/link/trecho por regex (sem parser HTML, sem API/chave de busca).
/// Frágil por natureza — o Google pode bloquear ou mudar o HTML a qualquer momento.
/// </summary>
public static class GoogleSearch
{
    private static readonly HttpClient _http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36");
        return client;
    }

    private static readonly Regex ResultBlock = new(
        @"<a href=""/url\?q=(?<url>[^""&]+)[^""]*""[^>]*>.*?<h3[^>]*>(?<title>.*?)</h3>",
        RegexOptions.Singleline);

    private static readonly Regex TagStripper = new("<.*?>", RegexOptions.Singleline);
    private static readonly Regex Whitespace = new(@"\s+");

    public static async Task<string> SearchAsync(string query, int maxResults = 5)
    {
        try
        {
            string url = $"https://www.google.com/search?q={Uri.EscapeDataString(query)}&num={maxResults}&hl=pt-BR";
            string html = await _http.GetStringAsync(url);

            var matches = ResultBlock.Matches(html);
            if (matches.Count == 0)
                return "Nenhum resultado encontrado (ou o Google bloqueou a busca automatizada).";

            var sb = new StringBuilder();
            int count = 0;
            foreach (Match m in matches)
            {
                if (count >= maxResults) break;

                string link = WebUtility.UrlDecode(m.Groups["url"].Value);
                string title = CleanHtml(m.Groups["title"].Value);

                int snippetStart = m.Index + m.Length;
                int snippetLen = Math.Min(1500, html.Length - snippetStart);
                string snippet = CleanHtml(html.Substring(snippetStart, snippetLen));
                if (snippet.Length > 220) snippet = snippet[..220] + "...";

                count++;
                sb.AppendLine($"{count}. {title}\n   {link}\n   {snippet}");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Erro ao buscar na internet: {ex.Message}";
        }
    }

    private static string CleanHtml(string raw)
    {
        string noTags = TagStripper.Replace(raw, " ");
        string decoded = WebUtility.HtmlDecode(noTags);
        return Whitespace.Replace(decoded, " ").Trim();
    }
}
