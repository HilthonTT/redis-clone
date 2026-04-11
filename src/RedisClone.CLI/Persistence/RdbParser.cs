using RedisClone.CLI.Storage;
using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace RedisClone.CLI.Persistence;

internal sealed class RdbParser
{
    private static readonly DateTime Epoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // One-byte lookahead — avoids seeking backwards on non-seekable streams.
    private byte? _peeked;

    public async Task<DataModel> ParseAsync(string backupFile)
    {
        byte[] fileBytes = await File.ReadAllBytesAsync(backupFile);

        VerifyChecksum(fileBytes, backupFile);

        using var stream = new MemoryStream(fileBytes, writable: false);

        var dataModel = new DataModel();
        await ParseHeaderAsync(stream, dataModel);
        await ParseMetadataAsync(stream, dataModel);
        await ParseDatabasesAsync(stream, dataModel);
        return dataModel;
    }

    private static async Task ParseHeaderAsync(Stream stream, DataModel dataModel)
    {
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

            if (next == 0xFF)
            {
                await ConsumePeekedAsync();
                var crc = new byte[8];
                await stream.ReadExactlyAsync(crc);
                return;
            }

            if (next != 0xFE) return;

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
        int _ = (int)await ReadLengthAsync(stream); // keys-with-expiry count

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

            if (expiresAt.HasValue)
            {
                // Compute remaining TTL as a delta from now.
                // If already expired, store with 0ms so the eviction timer cleans it up.
                long remainingMs = (long)(expiresAt.Value.ToUniversalTime() - DateTime.UtcNow).TotalMilliseconds;
                kvp[key] = StorageEntry.WithExpiry(value, Math.Max(remainingMs, 0));
            }
            else
            {
                kvp[key] = StorageEntry.Permanent(value);
            }
        }

        return kvp;
    }

    private async Task<(long Value, bool IsLength)> ReadLengthEncodedAsync(Stream stream)
    {
        byte first = await ReadByteAsync(stream);
        int type = (first & 0b1100_0000) >> 6;

        switch (type)
        {
            case 0b00:
                return (first & 0b0011_1111, true);

            case 0b01:
                byte second = await ReadByteAsync(stream);
                int len14 = ((first & 0b0011_1111) << 8) | second;
                return (len14, true);

            case 0b10:
                var buf32 = new byte[4];
                await stream.ReadExactlyAsync(buf32);
                return (BinaryPrimitives.ReadUInt32BigEndian(buf32), true);

            default:
                int intType = first & 0b0011_1111;
                int byteCount = intType switch
                {
                    0 => 1,
                    1 => 2,
                    2 => 4,
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

    private static void VerifyChecksum(ReadOnlySpan<byte> fileBytes, string backupFile)
    {
        // Redis writes 0x00 for the checksum when it's disabled.
        // A file shorter than 9 (header) + 8 (checksum) bytes is malformed regardless.
        if (fileBytes.Length < 17)
        {
            throw new InvalidDataException(
                $"RDB file '{backupFile}' is too short to contain a valid checksum.");
        }

        var payload = fileBytes[..^8];
        var stored = fileBytes[^8..];

        ulong storedCrc = BinaryPrimitives.ReadUInt64LittleEndian(stored);
        ulong computedCrc = Crc64.Compute(payload);

        // Redis writes all-zeros when checksum verification is disabled (rdbchecksum no).
        // Treat that as a valid "skip" rather than a failure.
        if (storedCrc == 0)
        {
            return;
        }

        if (storedCrc != computedCrc)
        {
            throw new InvalidDataException(
                $"RDB file '{backupFile}' failed CRC-64 integrity check. " +
                $"Expected 0x{computedCrc:X16}, got 0x{storedCrc:X16}. " +
                "The file may be corrupted or truncated.");
        }
    }
}
