using System.Net.Sockets;
using System.Text;

/// <summary>
/// Framing simples com prefixo de 4 bytes (int32 little-endian) indicando o tamanho da mensagem.
/// Garante que mensagens grandes (ex: ASCII art) cheguem completas.
/// </summary>
public static class SocketFraming
{
    public static async Task WriteAsync(NetworkStream stream, string message)
    {
        var body = Encoding.UTF8.GetBytes(message);
        var length = BitConverter.GetBytes(body.Length); // 4 bytes
        var packet = new byte[4 + body.Length];
        Buffer.BlockCopy(length, 0, packet, 0, 4);
        Buffer.BlockCopy(body, 0, packet, 4, body.Length);
        await stream.WriteAsync(packet);
    }

    /// <summary>Lê uma mensagem completa. Retorna null se a conexão fechou.</summary>
    public static async Task<string?> ReadAsync(NetworkStream stream)
    {
        var lenBuf = new byte[4];
        if (!await ReadExactAsync(stream, lenBuf, 4)) return null;

        int length = BitConverter.ToInt32(lenBuf, 0);
        if (length <= 0 || length > 10 * 1024 * 1024) return null; // máx 10 MB

        var body = new byte[length];
        if (!await ReadExactAsync(stream, body, length)) return null;

        return Encoding.UTF8.GetString(body);
    }

    private static async Task<bool> ReadExactAsync(NetworkStream stream, byte[] buffer, int count)
    {
        int offset = 0;
        while (offset < count)
        {
            int read = await stream.ReadAsync(buffer, offset, count - offset);
            if (read == 0) return false;
            offset += read;
        }
        return true;
    }
}
