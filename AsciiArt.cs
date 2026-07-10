using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;

[SupportedOSPlatform("windows")]

public static class AsciiArt
{
    // gradiente do escuro ao claro
    private const string Chars = " `░.-':_,^=;><+!rc*/z?sLTv)J7(|Fi{C}▒fI31tlu[neoZ5Yxjya]2ESwqkP6h9d4VpOGbUAKXHm8RD#$Bg0MNWQ%&▓@█";

    private static readonly Regex Pattern = new(@"^%(.+)%$");

    // 0x5100 = PropertyTagFrameDelay: array de int32, um por frame, em unidades de 1/100s
    private const int FrameDelayPropertyId = 0x5100;

    /// <summary>
    /// Se input for %caminho%, extrai o caminho do arquivo (sem checar extensão).
    /// Retorna null se não for esse padrão.
    /// </summary>
    public static string? ExtractPath(string input)
    {
        var match = Pattern.Match(input.Trim());
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    public static bool IsGif(string path) =>
        string.Equals(Path.GetExtension(path), ".gif", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Se input for %caminho% de uma imagem estática, converte e retorna o ASCII art.
    /// Retorna null se não for esse padrão, se o arquivo não existir, ou se for um GIF (use ConvertGif nesse caso).
    /// </summary>
    public static string? TryConvert(string input)
    {
        string? path = ExtractPath(input);
        if (path == null) return null;

        if (!File.Exists(path))
            return $"[AsciiArt] Arquivo não encontrado: {path}";

        if (IsGif(path)) return null;

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
        var (width, height) = TargetSize(original.Width, original.Height);

        using var bmp = new Bitmap(original, width, height);
        return string.Join('\n', RenderBitmap(bmp));
    }

    public static GifArt ConvertGif(string path)
    {
        using var image = Image.FromFile(path);
        var dimension = new FrameDimension(image.FrameDimensionsList[0]);
        int frameCount = image.GetFrameCount(dimension);

        byte[]? delayBytes = null;
        try { delayBytes = image.GetPropertyItem(FrameDelayPropertyId)?.Value; } catch { }

        var (width, height) = TargetSize(image.Width, image.Height);

        var frameLines = new List<string[]>(frameCount);
        var delaysMs = new List<int>(frameCount);

        for (int i = 0; i < frameCount; i++)
        {
            image.SelectActiveFrame(dimension, i);
            using var frameBmp = new Bitmap(width, height);
            using (var g = Graphics.FromImage(frameBmp))
                g.DrawImage(image, 0, 0, width, height);

            frameLines.Add(RenderBitmap(frameBmp));

            int delayMs = 100;
            int offset = i * 4;
            if (delayBytes != null && offset + 3 < delayBytes.Length)
            {
                int raw = BitConverter.ToInt32(delayBytes, offset);
                delayMs = Math.Max(20, raw * 10);
            }
            delaysMs.Add(delayMs);
        }

        return new GifArt(frameLines, delaysMs, height);
    }

    private static (int Width, int Height) TargetSize(int originalWidth, int originalHeight)
    {
        int width = Math.Min(Console.WindowWidth - 1, 60);
        // compensar proporção: chars são ~2x mais altos que largos
        int height = (int)(width * ((float)originalHeight / originalWidth) * 0.5f);
        return (width, Math.Max(1, height));
    }

    private static string[] RenderBitmap(Bitmap bmp)
    {
        var lines = new string[bmp.Height];
        var sb = new StringBuilder();
        for (int y = 0; y < bmp.Height; y++)
        {
            sb.Clear();
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
            lines[y] = sb.ToString();
        }
        return lines;
    }
}

/// <summary>Ascii art animado extraído de um GIF: um array de linhas por frame, todos com a mesma altura.</summary>
public sealed record GifArt(List<string[]> FrameLines, List<int> DelaysMs, int Height);
