using RedisClone.CLI.Storage;
using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace RedisClone.CLI.Persistence;

internal sealed class RdbParser
{
    private static readonly DateTime Epoch =
        new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // One-byte lookahead — avoids seeking backwards on non-seekable streams.
    private byte? _peeked;

    public async Task<DataModel> ParseAsync(string backupFile)
    {
        await using var fileStream = new FileStream(
            backupFile, FileMode.Open, FileAccess.Read,
            FileShare.Read, bufferSize: 4096, useAsync: true);

        var dataModel = new DataModel();
        await ParseHeaderAsync(fileStream, dataModel);
        await ParseMetadataAsync(fileStream, dataModel);
        await ParseDatabasesAsync(fileStream, dataModel);
        return dataModel;
    }

    private static async Task ParseHeaderAsync(Stream stream, DataModel dataModel)
    {
        // Each section gets its own stack-allocated or pooled buffer.
        var header = new byte[9];
        await stream.ReadExactlyAsync(header);

        if (Encoding.ASCII.GetString(header, 0, 5) != "REDIS")
        {
            throw new InvalidDataException(
                $"Missing REDIS magic string, got: {Encoding.ASCII.GetString(header, 0, 5)}");
        }

        string versionStr = Encoding.ASCII.GetString(header, 5, 4);
        if (!int.TryParse(versionStr, out int version))
        {
            throw new InvalidDataException($"Invalid RDB version: '{versionStr}'");
        }

        dataModel.RdbVersion = version;
    }

    private async Task ParseMetadataAsync(Stream stream, DataModel dataModel)
    {
        while (true)
        {
            byte next = await PeekByteAsync(stream);
            if (next != 0xFA) return;

            await ConsumePeekedAsync();
            string name = await ReadStringAsync(stream);
            string value = await ReadStringAsync(stream);
            dataModel.Metadata.Add((name, value));
        }
    }

    private async Task ParseDatabasesAsync(Stream stream, DataModel dataModel)
    {
        while (true)
        {
            byte next = await PeekByteAsync(stream);

            if (next == 0xFF) // EOF marker
            {
                await ConsumePeekedAsync();
                // Remaining 8 bytes are the CRC64 checksum — skip for now.
                var crc = new byte[8];
                await stream.ReadExactlyAsync(crc);
                return;
            }

            if (next != 0xFE) return; // Unexpected — stop gracefully.

            await ConsumePeekedAsync();
            int dbNumber = (int)await ReadLengthAsync(stream);
            var entries = await ParseDatabaseAsync(stream);
            dataModel.Databases[dbNumber] = entries;
        }
    }

    private async Task<Dictionary<string, StorageEntry>> ParseDatabaseAsync(Stream stream)
    {
        byte hashTableMarker = await ReadByteAsync(stream);
        if (hashTableMarker != 0xFB)
        {
            throw new InvalidDataException(
                $"Expected hash table marker 0xFB, got 0x{hashTableMarker:X2}");
        }

        int totalKeys = (int)await ReadLengthAsync(stream);
        int _ = (int)await ReadLengthAsync(stream); // keys-with-expiry count (informational)

        var kvp = new Dictionary<string, StorageEntry>(totalKeys);

        for (int i = 0; i < totalKeys; i++)
        {
            DateTime? expiresAt = null;
            byte valueTypeByte = await ReadByteAsync(stream);

            if (valueTypeByte is 0xFC or 0xFD)
            {
                expiresAt = valueTypeByte switch
                {
                    0xFC => Epoch.AddMilliseconds(await ReadFixedLongAsync(stream, 8)),
                    0xFD => Epoch.AddSeconds(await ReadFixedLongAsync(stream, 4)),
                    _ => null
                };
                valueTypeByte = await ReadByteAsync(stream);
            }

            if (valueTypeByte != 0x00)
            {
                throw new InvalidDataException(
                    $"Expected value type 0x00 (string), got 0x{valueTypeByte:X2}");
            }

            string key = await ReadStringAsync(stream);
            string value = await ReadStringAsync(stream);
            kvp[key] = StorageEntry.WithExpiry(value, expiresAt.HasValue ? expiresAt.Value.Millisecond : 0);
        }

        return kvp;
    }


    /// <summary>
    /// Reads an RDB length-encoded integer.
    /// Returns the length for string/array sizing, or the encoded integer value.
    /// </summary>
    private async Task<(long Value, bool IsLength)> ReadLengthEncodedAsync(Stream stream)
    {
        byte first = await ReadByteAsync(stream);
        int type = (first & 0b1100_0000) >> 6;

        switch (type)
        {
            case 0b00:
                // 6-bit length
                return (first & 0b0011_1111, true);

            case 0b01:
                // 14-bit length (big-endian)
                byte second = await ReadByteAsync(stream);
                int len14 = ((first & 0b0011_1111) << 8) | second;
                return (len14, true);

            case 0b10:
                // 32-bit big-endian length
                var buf32 = new byte[4];
                await stream.ReadExactlyAsync(buf32);
                return (BinaryPrimitives.ReadUInt32BigEndian(buf32), true);

            default: // 0b11 — special integer encoding
                int intType = first & 0b0011_1111;
                int byteCount = intType switch
                {
                    0 => 1, // 8-bit int
                    1 => 2, // 16-bit int
                    2 => 4, // 32-bit int
                    _ => throw new InvalidDataException(
                        $"Unsupported special encoding: {intType}")
                };
                long value = await ReadFixedLongAsync(stream, byteCount);
                return (value, false);
        }
    }

    private async Task<string> ReadStringAsync(Stream stream)
    {
        var (value, isLength) = await ReadLengthEncodedAsync(stream);

        if (!isLength)
            return value.ToString();

        int length = (int)value;
        if (length == 0) return string.Empty;

        // Rent a buffer to avoid per-call allocation for typical string sizes.
        byte[] rented = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            await stream.ReadExactlyAsync(rented.AsMemory(0, length));
            return Encoding.UTF8.GetString(rented, 0, length);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private async Task<long> ReadLengthAsync(Stream stream)
    {
        var (value, _) = await ReadLengthEncodedAsync(stream);
        return value;
    }

    private static async Task<long> ReadFixedLongAsync(Stream stream, int byteCount)
    {
        var buf = new byte[byteCount];
        await stream.ReadExactlyAsync(buf);

        long result = 0;
        for (int i = 0; i < byteCount; i++)
        {
            result |= (long)buf[i] << (8 * i);
        }

        return result;
    }

    private async Task<byte> ReadByteAsync(Stream stream)
    {
        if (_peeked.HasValue)
        {
            byte b = _peeked.Value;
            _peeked = null;
            return b;
        }

        var buf = new byte[1];
        await stream.ReadExactlyAsync(buf);
        return buf[0];
    }

    private async Task<byte> PeekByteAsync(Stream stream)
    {
        if (!_peeked.HasValue)
        {
            _peeked = await ReadByteAsync(stream);
        }
        return _peeked.Value;
    }

    private Task ConsumePeekedAsync()
    {
        _peeked = null;
        return Task.CompletedTask;
    }
}
