using System.Buffers;
using System.Text;

namespace RedisClone.Client.Protocol;

/// <summary>
/// Serializes Redis commands into the RESP bulk-string-array wire format.
/// Thread-safe — each call allocates its own buffer.
/// </summary>
internal static class RespWriter
{
    /// <summary>
    /// Encodes a command with arguments into a RESP array of bulk strings.
    /// Example: ["SET", "key", "value"] → *3\r\n$3\r\nSET\r\n$3\r\nkey\r\n$5\r\nvalue\r\n
    /// </summary>
    public static ReadOnlyMemory<byte> Encode(params string[] parts)
    {
        // Pre-calculate size to avoid reallocations
        int estimatedSize = 16; // *N\r\n header
        foreach (string part in parts)
        {
            estimatedSize += 16 + Encoding.UTF8.GetByteCount(part); // $N\r\n<data>\r\n
        }

        var buffer = new ArrayBufferWriter<byte>(estimatedSize);

        // Array header: *<count>\r\n
        WriteAscii(buffer, $"*{parts.Length}\r\n");

        foreach (string part in parts)
        {
            int byteCount = Encoding.UTF8.GetByteCount(part);

            // Bulk string header: $<length>\r\n
            WriteAscii(buffer, $"${byteCount}\r\n");

            // Payload
            var span = buffer.GetSpan(byteCount);
            int written = Encoding.UTF8.GetBytes(part, span);
            buffer.Advance(written);

            // Trailing \r\n
            WriteAscii(buffer, "\r\n");
        }

        return buffer.WrittenMemory;
    }

    private static void WriteAscii(ArrayBufferWriter<byte> buffer, string text)
    {
        var span = buffer.GetSpan(text.Length);
        int written = Encoding.ASCII.GetBytes(text, span);
        buffer.Advance(written);
    }
}
