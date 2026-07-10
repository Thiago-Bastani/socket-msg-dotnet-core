/// <summary>
/// Serializa/desserializa um pedido de "/grok" vindo de um client, para que o SERVER (único que
/// de fato chama a Grok API) o reconheça entre as mensagens normais de chat.
/// </summary>
public static class GrokRequestCodec
{
    private const string Marker = "GROKREQ";

    public static string Encode(string name, string question) => $"{Marker}\n{name}\n{question}";

    public static bool TryDecode(string raw, out string name, out string question)
    {
        name = "";
        question = "";

        if (!raw.StartsWith(Marker, StringComparison.Ordinal)) return false;

        var parts = raw.Split('\n', 3);
        if (parts.Length < 3) return false;

        name = parts[1];
        question = parts[2];
        return true;
    }
}
