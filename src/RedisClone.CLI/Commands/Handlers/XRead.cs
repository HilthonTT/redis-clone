using RedisClone.CLI.Commands.Handlers.Validation;
using RedisClone.CLI.Models;
using RedisClone.CLI.Options;
using RedisClone.CLI.Storage;
using RedisClone.CLI.Subscriptions;
using System.Threading.Channels;

namespace RedisClone.CLI.Commands.Handlers;

/// <summary>
/// XREAD [COUNT count] [BLOCK milliseconds] STREAMS key [key ...] id [id ...]
///
/// <code>
/// XREAD STREAMS mystream 0              → all entries after 0
/// XREAD COUNT 5 STREAMS mystream 0      → up to 5 entries
/// XREAD BLOCK 5000 STREAMS mystream $   → block 5s for new entries
/// </code>
/// </summary>
[Argument(min: 3)]
internal sealed class XRead(
    AppSettings settings,
    StreamStorage storage,
    PubSub pubSub) : BaseCommandHandler(settings)
{
    public override CommandType CommandType => CommandType.XRead;

    public override bool LongOperation => true;

    public override bool SupportsReplication => false;

    protected override async Task<RedisValue> HandleSpecificAsync(Command command, ClientConnection connection)
    {
        if (!TryParseArguments(command.Arguments, out int? count, out long? blockMs,
            out string[]? keys, out string[]? ids, out string? error))
        {
            return RedisValue.ToError(error!);
        }

        // Try immediate read first.
        var result = ReadFromStreams(keys!, ids!, count);
        if (result.Count > 0)
        {
            return FormatXReadResult(result);
        }

        // If no BLOCK, return nil.
        if (!blockMs.HasValue)
        {
            return RedisValue.NullBulkStringArray;
        }

        // BLOCK: subscribe and wait for new entries.
        return await BlockForEntriesAsync(keys!, ids!, count, blockMs.Value, connection);
    }

    private List<(string Key, List<RedisStream.StreamEntry> Entries)> ReadFromStreams(
        string[] keys, string[] ids, int? count)
    {
        var result = new List<(string, List<RedisStream.StreamEntry>)>();

        for (int i = 0; i < keys.Length; i++)
        {
            // "$" means "only new entries from now on" — nothing to return yet
            if (ids[i] == "$")
            {
                continue;
            }

            var entries = storage.ReadAfter(keys[i], ids[i], count);
            if (entries.Count > 0)
            {
                result.Add((keys[i], entries));
            }
        }

        return result;
    }

    private async Task<RedisValue> BlockForEntriesAsync(
        string[] keys, string[] ids, int? count, long blockMs, ClientConnection connection)
    {
        var channel = Channel.CreateBounded<PubSubMessage>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

        // Subscribe to all stream keys for new entries.
        foreach (string key in keys)
        {
            pubSub.Subscribe(EventType.StreamAppended, key, connection.Id, channel);
        }

        try
        {
            using var cts = blockMs > 0
                ? new CancellationTokenSource(TimeSpan.FromMilliseconds(blockMs))
                : new CancellationTokenSource();

            await foreach (var _ in channel.Reader.ReadAllAsync(cts.Token))
            {
                var result = ReadFromStreams(keys, ids, count);
                if (result.Count > 0)
                {
                    return FormatXReadResult(result);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Timeout — return nil
        }
        finally
        {
            foreach (string key in keys)
            {
                pubSub.Unsubscribe(EventType.StreamAppended, key, connection.Id);
            }
            channel.Writer.TryComplete();
        }

        return RedisValue.NullBulkStringArray;
    }

    private static RedisValue FormatXReadResult(
        List<(string Key, List<RedisStream.StreamEntry> Entries)> streams)
    {
        var outer = new List<RedisValue>(streams.Count);

        foreach (var (key, entries) in streams)
        {
            // Each stream result is [streamKey, [[id, [field, value, ...]], ...]]
            var streamResult = RedisValue.FromArray([
                RedisValue.ToBulkString(key),
                XRange.FormatStreamEntries(entries),
            ]);
            outer.Add(streamResult);
        }

        return RedisValue.FromArray(outer);
    }

    private static bool TryParseArguments(
        string[] args, 
        out int? count, 
        out long? blockMs,
        out string[]? keys, 
        out string[]? ids, 
        out string? error)
    {
        count = null;
        blockMs = null;
        keys = null;
        ids = null;
        error = null;

        int cursor = 0;

        // Parse optional COUNT and BLOCK before STREAMS keyword
        while (cursor < args.Length)
        {
            if (args[cursor].Equals("COUNT", StringComparison.OrdinalIgnoreCase))
            {
                if (cursor + 1 >= args.Length || !int.TryParse(args[cursor + 1], out int c))
                {
                    error = "ERR value is not an integer or out of range";
                    return false;
                }
                count = c;
                cursor += 2;
            }
            else if (args[cursor].Equals("BLOCK", StringComparison.OrdinalIgnoreCase))
            {
                if (cursor + 1 >= args.Length || !long.TryParse(args[cursor + 1], out long b))
                {
                    error = "ERR value is not an integer or out of range";
                    return false;
                }
                blockMs = b;
                cursor += 2;
            }
            else if (args[cursor].Equals("STREAMS", StringComparison.OrdinalIgnoreCase))
            {
                cursor++;
                break;
            }
            else
            {
                error = $"ERR Unrecognized XREAD option '{args[cursor]}'";
                return false;
            }
        }

        // Remaining args are: key [key ...] id [id ...]
        // Split evenly: first half are keys, second half are IDs
        int remaining = args.Length - cursor;
        if (remaining == 0 || remaining % 2 != 0)
        {
            error = "ERR Unbalanced XREAD list of streams: for each stream key an ID must be specified";
            return false;
        }

        int streamCount = remaining / 2;
        keys = args[cursor..(cursor + streamCount)];
        ids = args[(cursor + streamCount)..];
        return true;
    }
}
