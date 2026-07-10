using System.Text;

/// <summary>
/// Serializa/desserializa um GifArt animado em uma única string, para trafegar
/// pelo SocketFraming como qualquer outra mensagem de chat.
/// </summary>
public static class GifMessageCodec
{
    private const string Marker = "GIFANIM";

    public static string Encode(string header, GifArt gif)
    {
        var sb = new StringBuilder();
        sb.Append(Marker).Append('\n');
        sb.Append(header).Append('\n');
        sb.Append(gif.FrameLines.Count).Append('|')
          .Append(gif.Height).Append('|')
          .Append(string.Join(',', gif.DelaysMs)).Append('\n');

        foreach (var frame in gif.FrameLines)
            foreach (var line in frame)
                sb.Append(line).Append('\n');

        return sb.ToString().TrimEnd('\n');
    }

    public static bool TryDecode(string raw, out string header, out List<string[]> frames, out List<int> delaysMs)
    {
        header = "";
        frames = new List<string[]>();
        delaysMs = new List<int>();

        if (!raw.StartsWith(Marker, StringComparison.Ordinal)) return false;

        // raw = "GIFANIM\n{header}\n{count}|{height}|{delays}\n{frame lines...}"
        var lines = raw.Split('\n');
        if (lines.Length < 3) return false;

        header = lines[1];
        var metaParts = lines[2].Split('|');
        if (metaParts.Length != 3) return false;
        if (!int.TryParse(metaParts[0], out int count)) return false;
        if (!int.TryParse(metaParts[1], out int height)) return false;

        delaysMs = metaParts[2].Split(',').Select(s => int.TryParse(s, out int d) ? d : 100).ToList();

        int idx = 3;
        for (int f = 0; f < count; f++)
        {
            var frameLines = new string[height];
            for (int y = 0; y < height; y++)
                frameLines[y] = idx < lines.Length ? lines[idx++] : "";
            frames.Add(frameLines);
        }

        return true;
    }
}
