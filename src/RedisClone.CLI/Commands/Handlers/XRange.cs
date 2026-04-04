using RedisClone.CLI.Commands.Handlers.Validation;
using RedisClone.CLI.Models;
using RedisClone.CLI.Options;
using RedisClone.CLI.Storage;

namespace RedisClone.CLI.Commands.Handlers;

[Argument(min: 3, max: 5)]
internal sealed class XRange(AppSettings settings, StreamStorage storage) : BaseCommandHandler(settings)
{
    public override bool SupportsReplication => false;

    public override CommandType CommandType => CommandType.XRange;

    protected override RedisValue HandleSpecific(Command command, ClientConnection connection)
    {
        string streamKey = command.Arguments[0];
        string start = command.Arguments[1];
        string end = command.Arguments[2];

        int? count = null;
        if (command.Arguments.Length == 5 &&
            command.Arguments[3].Equals("COUNT", StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(command.Arguments[4], out int c))
            {
                return RedisValue.ToError("ERR value is not an integer or out of range");
            }
            count = c;
        }

        var entries = storage.Range(streamKey, start, end);

        if (count.HasValue && entries.Count > count.Value)
        {
            entries = entries.Take(count.Value).ToList();
        }

        if (entries.Count == 0)
        {
            return RedisValue.EmptyBulkStringArray;
        }

        return FormatStreamEntries(entries);
    }

    /// <summary>
    /// Formats stream entries as a RESP array of [id, [field, value, ...]] pairs.
    /// </summary>
    internal static RedisValue FormatStreamEntries(List<RedisStream.StreamEntry> entries)
    {
        var outer = new List<RedisValue>(entries.Count);

        foreach (var entry in entries)
        {
            // Each entry is a 2-element array: [id, [field, value, ...]]
            var fieldValues = new List<string>(entry.Values.Count * 2);
            foreach (var (field, value) in entry.Values)
            {
                fieldValues.Add(field);
                fieldValues.Add(value);
            }

            var entryArray = RedisValue.FromArray([
                RedisValue.ToBulkString(entry.Id),
                RedisValue.ToBulkStringArray(fieldValues),
            ]);

            outer.Add(entryArray);
        }

        return RedisValue.FromArray(outer);
    }
}
