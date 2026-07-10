public static class GrokConfig
{
    private const string EnvVarName = "GROK_API_KEY";
    private const string KeyFileName = "grok.key";

    /// <summary>Lê a chave da variável de ambiente GROK_API_KEY, ou do arquivo grok.key (gitignored) na pasta do executável/projeto.</summary>
    public static string? GetApiKey()
    {
        string? fromEnv = Environment.GetEnvironmentVariable(EnvVarName);
        if (!string.IsNullOrWhiteSpace(fromEnv)) return fromEnv.Trim();

        foreach (var dir in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            string path = Path.Combine(dir, KeyFileName);
            if (File.Exists(path))
            {
                string fromFile = File.ReadAllText(path).Trim();
                if (!string.IsNullOrWhiteSpace(fromFile)) return fromFile;
            }
        }

        return null;
    }
}
