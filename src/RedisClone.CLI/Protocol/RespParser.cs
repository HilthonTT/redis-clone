using System.Buffers;
using System.IO.Pipelines;
using System.Text;

namespace RedisClone.CLI.Protocol;

/// <summary>
/// Streaming RESP parser that reads from a <see cref="PipeReader"/>.
/// Handles partial TCP reads, binary-safe bulk strings, and command pipelining.
/// </summary>
internal static class RespParser
{
    private static readonly byte[] CrLf = "\r\n"u8.ToArray();

    /// <summary>
    /// Reads a single complete RESP value from the pipe.
    /// Returns null if the connection is closed.
    /// </summary>
    public static async Task<RespResult?> ReadAsync(PipeReader reader, CancellationToken cancellationToken = default)
    {
        while (true)
        {
            ReadResult readResult = await reader.ReadAsync(cancellationToken);
            ReadOnlySequence<byte> buffer = readResult.Buffer;

            if (TryParse(ref buffer, out RespResult? value))
            {
                reader.AdvanceTo(buffer.Start);
                return value;
            }

            reader.AdvanceTo(buffer.Start, buffer.End);

            if (readResult.IsCompleted)
            {
                return null;
            }
        }
    }

    private static bool TryParse(ref ReadOnlySequence<byte> buffer, out RespResult? value)
    {
        value = null;

        if (buffer.Length == 0)
        {
            return false;
        }

        byte prefix = buffer.FirstSpan[0];

        return prefix switch
        {
            (byte)'+' => TryParseSimpleString(ref buffer, out value),
            (byte)'-' => TryParseError(ref buffer, out value),
            (byte)':' => TryParseInteger(ref buffer, out value),
            (byte)'$' => TryParseBulkString(ref buffer, out value),
            (byte)'*' => TryParseArray(ref buffer, out value),
            _ => throw new InvalidDataException(
                $"Unexpected RESP prefix byte: 0x{prefix:X2} ('{(char)prefix}')")
        };
    }

    private static bool TryParseSimpleString(ref ReadOnlySequence<byte> buffer, out RespResult? value)
    {
        value = null;
        if (!TryReadLine(buffer, out var line, out var consumed))
        {
            return false;
        }

        value = RespResult.SimpleString(DecodeUtf8(line.Slice(1)));
        buffer = buffer.Slice(consumed);
        return true;
    }

    private static bool TryParseError(ref ReadOnlySequence<byte> buffer, out RespResult? value)
    {
        value = null;
        if (!TryReadLine(buffer, out var line, out var consumed))
        {
            return false;
        }

        value = RespResult.Error(DecodeUtf8(line.Slice(1)));
        buffer = buffer.Slice(consumed);

        return true;
    }

    private static bool TryParseInteger(ref ReadOnlySequence<byte> buffer, out RespResult? value)
    {
        value = null;
        if (!TryReadLine(buffer, out var line, out var consumed))
        {
            return false;
        }

        string text = DecodeUtf8(line.Slice(1));
        if (!long.TryParse(text, out long num))
        {
            throw new InvalidDataException($"Invalid RESP integer: '{text}'");
        }

        value = RespResult.Integer(num);
        buffer = buffer.Slice(consumed);
        return true;
    }

    private static bool TryParseBulkString(ref ReadOnlySequence<byte> buffer, out RespResult? value)
    {
        value = null;
        if (!TryReadLine(buffer, out var lengthLine, out var afterLength))
        {
            return false;
        }

        string lengthText = DecodeUtf8(lengthLine.Slice(1));
        if (!int.TryParse(lengthText, out int length))
        {
            throw new InvalidDataException($"Invalid bulk string length: '{lengthText}'");
        }

        if (length < 0)
        {
            value = RespResult.Null();
            buffer = buffer.Slice(afterLength);
            return true;
        }

        var remaining = buffer.Slice(afterLength);
        long needed = length + CrLf.Length;
        if (remaining.Length < needed)
        {
            return false;
        }

        var payload = remaining.Slice(0, length);
        value = RespResult.BulkString(DecodeUtf8(payload));
        buffer = buffer.Slice(remaining.GetPosition(needed));
        return true;
    }

    private static bool TryParseArray(ref ReadOnlySequence<byte> buffer, out RespResult? value)
    {
        value = null;
        var savedBuffer = buffer;

        if (!TryReadLine(buffer, out var countLine, out var afterCount))
        {
            return false;
        }

        string countText = DecodeUtf8(countLine.Slice(1));
        if (!int.TryParse(countText, out int count))
        {
            throw new InvalidDataException($"Invalid array count: '{countText}'");
        }

        if (count < 0)
        {
            value = RespResult.Null();
            buffer = buffer.Slice(afterCount);
            return true;
        }

        buffer = buffer.Slice(afterCount);
        var elements = new RespResult[count];

        for (int i = 0; i < count; i++)
        {
            if (!TryParse(ref buffer, out RespResult? element))
            {
                buffer = savedBuffer;
                return false;
            }
            elements[i] = element!;
        }

        value = RespResult.Array(elements);
        return true;
    }

    private static bool TryReadLine(
        ReadOnlySequence<byte> buffer,
        out ReadOnlySequence<byte> line,
        out SequencePosition consumed)
    {
        line = default;
        consumed = default;

        var reader = new SequenceReader<byte>(buffer);
        if (!reader.TryReadTo(out ReadOnlySequence<byte> found, CrLf.AsSpan()))
        {
            return false;
        }

        line = found;
        consumed = reader.Position;

        return true;
    }

    private static string DecodeUtf8(ReadOnlySequence<byte> sequence)
    {
        if (sequence.IsSingleSegment)
        {
            return Encoding.UTF8.GetString(sequence.FirstSpan);
        }

        int length = (int)sequence.Length;
        byte[] rented = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            sequence.CopyTo(rented);
            return Encoding.UTF8.GetString(rented, 0, length);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }
}