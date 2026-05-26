using System.Drawing;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;

[SupportedOSPlatform("windows")]

public static class AsciiArt
{
    // gradiente do escuro ao claro
    private const string Chars = " `.-':_,^=;><+!rc*/z?sLTv)J7(|Fi{C}fI31tlu[neoZ5Yxjya]2ESwqkP6h9d4VpOGbUAKXHm8RD#$Bg0MNWQ%&@";

    private static readonly Regex Pattern = new(@"^%(.+)%$");

    /// <summary>
    /// Se input for %caminho%, converte a imagem e retorna o ASCII art.
    /// Retorna null se não for esse padrão.
    /// </summary>
    public static string? TryConvert(string input)
    {
        var match = Pattern.Match(input.Trim());
        if (!match.Success) return null;

        string path = match.Groups[1].Value.Trim();
        if (!File.Exists(path))
            return $"[AsciiArt] Arquivo não encontrado: {path}";

        try
        {
            return Convert(path);
        }
        catch (Exception ex)
        {
            return $"[AsciiArt] Erro: {ex.Message}";
        }
    }

    public static string Convert(string path)
    {
        using var original = new Bitmap(path);

        int width = Math.Min(Console.WindowWidth - 1, 120);
        // compensar proporção: chars são ~2x mais altos que largos
        int height = (int)(width * ((float)original.Height / original.Width) * 0.5f);
        height = Math.Max(1, height);

        using var bmp = new Bitmap(original, width, height);

        var sb = new StringBuilder();
        for (int y = 0; y < bmp.Height; y++)
        {
            for (int x = 0; x < bmp.Width; x++)
            {
                var p = bmp.GetPixel(x, y);

                // pixel transparente → trata como branco (espaço no ASCII)
                float alpha = p.A / 255f;
                float brightness = (p.R * 0.299f + p.G * 0.587f + p.B * 0.114f) / 255f;
                brightness = brightness * alpha + 1f * (1f - alpha);

                int idx = Chars.Length - 1 - (int)(brightness * (Chars.Length - 1));
                sb.Append(Chars[idx]);
            }
            if (y < bmp.Height - 1) sb.Append('\n');
        }

        return sb.ToString();
    }
}
