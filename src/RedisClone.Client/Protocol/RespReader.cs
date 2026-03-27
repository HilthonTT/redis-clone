using RedisClone.Client.Exceptions;
using System.Buffers;
using System.IO.Pipelines;
using System.Text;

namespace RedisClone.Client.Protocol;

/// <summary>
/// Reads RESP (Redis Serialization Protocol) values from a <see cref="PipeReader"/>.
/// Handles partial TCP reads, binary-safe bulk strings, and nested arrays.
/// </summary>
internal sealed class RespReader
{
    private static readonly byte[] CrLf = "\r\n"u8.ToArray();

    /// <summary>
    /// Reads a single complete RESP value from the pipe.
    /// Supports: Simple Strings (+), Errors (-), Integers (:), Bulk Strings ($), Arrays (*).
    /// </summary>
    public static async Task<RespValue> ReadAsync(PipeReader reader, CancellationToken cancellationToken = default)
    {
        while (true)
        {
            ReadResult readResult = await reader.ReadAsync(cancellationToken);
            ReadOnlySequence<byte> buffer = readResult.Buffer;

            if (TryParse(ref buffer, out RespValue? value))
            {
                reader.AdvanceTo(buffer.Start);
                return value!;
            }

            // Tell the pipe we examined everything but consumed nothing yet.
            reader.AdvanceTo(buffer.Start, buffer.End);

            if (readResult.IsCompleted)
            {
                throw new IOException("Connection closed while reading RESP value.");
            }
        }
    }

    private static bool TryParse(ref ReadOnlySequence<byte> buffer, out RespValue? value)
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
            _ => throw new ProtocolViolationException(
                $"Unexpected RESP prefix byte: 0x{prefix:X2} ('{(char)prefix}')")
        };
    }

    private static bool TryParseSimpleString(ref ReadOnlySequence<byte> buffer, out RespValue? value)
    {
        value = null;
        if (!TryReadLine(buffer, out ReadOnlySequence<byte> line, out SequencePosition consumed))
        {
            return false;
        }

        // Skip the '+' prefix
        string text = DecodeUtf8(line.Slice(1));
        value = RespValue.SimpleString(text);
        buffer = buffer.Slice(consumed);
        return true;
    }

    private static bool TryParseError(ref ReadOnlySequence<byte> buffer, out RespValue? value)
    {
        value = null;
        if (!TryReadLine(buffer, out ReadOnlySequence<byte> line, out SequencePosition consumed))
        {
            return false;
        }

        string text = DecodeUtf8(line.Slice(1));
        value = RespValue.Error(text);
        buffer = buffer.Slice(consumed);
        return true;
    }

    private static bool TryParseInteger(ref ReadOnlySequence<byte> buffer, out RespValue? value)
    {
        value = null;
        if (!TryReadLine(buffer, out ReadOnlySequence<byte> line, out SequencePosition consumed))
        {
            return false;
        }

        string text = DecodeUtf8(line.Slice(1));
        if (!long.TryParse(text, out long num))
        {
            throw new ProtocolViolationException($"Invalid RESP integer: '{text}'");
        }

        value = RespValue.Integer(num);
        buffer = buffer.Slice(consumed);
        return true;
    }

    private static bool TryParseBulkString(ref ReadOnlySequence<byte> buffer, out RespValue? value)
    {
        value = null;
        if (!TryReadLine(buffer, out ReadOnlySequence<byte> lengthLine, out SequencePosition afterLength))
        {
            return false;
        }

        string lengthText = DecodeUtf8(lengthLine.Slice(1));
        if (!int.TryParse(lengthText, out int length))
        {
            throw new ProtocolViolationException($"Invalid bulk string length: '{lengthText}'");
        }

        // $-1\r\n is the null bulk string
        if (length < 0)
        {
            value = RespValue.Null();
            buffer = buffer.Slice(afterLength);
            return true;
        }

        var remaining = buffer.Slice(afterLength);

        // Need: <length bytes> + \r\n
        long needed = length + CrLf.Length;
        if (remaining.Length < needed)
            return false;

        var payload = remaining.Slice(0, length);
        string text = DecodeUtf8(payload);

        value = RespValue.BulkString(text);
        buffer = buffer.Slice(remaining.GetPosition(needed));
        return true;
    }

    private static bool TryParseArray(ref ReadOnlySequence<byte> buffer, out RespValue? value)
    {
        value = null;

        // Save start position so we can restore on incomplete parse
        var savedBuffer = buffer;

        if (!TryReadLine(buffer, out ReadOnlySequence<byte> countLine, out SequencePosition afterCount))
        {
            return false;
        }

        string countText = DecodeUtf8(countLine.Slice(1));
        if (!int.TryParse(countText, out int count))
        {
            throw new ProtocolViolationException($"Invalid array count: '{countText}'");
        }

        // *-1 is null array, *0 is empty array
        if (count < 0)
        {
            value = RespValue.NullArray();
            buffer = buffer.Slice(afterCount);
            return true;
        }

        buffer = buffer.Slice(afterCount);
        var elements = new RespValue[count];

        for (int i = 0; i < count; i++)
        {
            if (!TryParse(ref buffer, out RespValue? element))
            {
                buffer = savedBuffer; // Restore — need more data
                return false;
            }
            elements[i] = element!;
        }

        value = RespValue.Array(elements);
        return true;
    }

    /// <summary>
    /// Finds the next \r\n in the buffer and returns everything before it.
    /// </summary>
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

        // Multi-segment: copy to contiguous buffer
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
