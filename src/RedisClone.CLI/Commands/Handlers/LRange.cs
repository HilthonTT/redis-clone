using RedisClone.CLI.Commands.Handlers.Validation;
using RedisClone.CLI.Models;
using RedisClone.CLI.Options;
using RedisClone.CLI.Storage;
using System.Net.Sockets;

namespace RedisClone.CLI.Commands.Handlers;

/// <summary>
/// Handles the Redis <c>LRANGE</c> command — returns a slice of a list
/// between two indices, inclusive.
/// </summary>
/// <remarks>
/// Supports negative indices: <c>-1</c> is the last element, <c>-2</c> is second-to-last, etc.
/// If the key does not exist, an empty array is returned (not an error).
///
/// <code>
/// LRANGE mylist 0 -1   → all elements
/// LRANGE mylist 0  1   → first two elements
/// LRANGE mylist -2 -1  → last two elements
/// LRANGE missing 0 -1  → (empty array)
/// </code>
/// </remarks>
[Argument(min: 3, max: 3)]
internal sealed class LRange(AppSettings settings, ListStorage listStorage) : BaseCommandHandler(settings)
{
    public override CommandType CommandType => CommandType.LRange;

    /// <summary>
    /// Executes <c>LRANGE key start end</c>.
    /// </summary>
    /// <param name="command">
    /// Expected arguments:
    /// <list type="bullet">
    ///   <item><c>Arguments[0]</c> — the list key, e.g. <c>"mylist"</c></item>
    ///   <item><c>Arguments[1]</c> — start index (inclusive), e.g. <c>"0"</c> or <c>"-2"</c></item>
    ///   <item><c>Arguments[2]</c> — end index (inclusive), e.g. <c>"3"</c> or <c>"-1"</c></item>
    /// </list>
    /// </param>
    /// <param name="socket">The client socket — unused here but required by the base interface.</param>
    /// <returns>
    /// A RESP bulk string array of the elements in the specified range.
    /// Returns an empty array if the key doesn't exist, the list is empty,
    /// or the range resolves to zero or fewer elements.
    /// Returns a RESP error if start or end are not valid integers.
    /// </returns>
    /// <example>
    /// List: <c>mylist = ["a", "b", "c", "d", "e"]</c>
    ///
    /// Input:  <c>LRANGE mylist 0 2</c>
    /// Output: <c>["a", "b", "c"]</c>
    ///
    /// Input:  <c>LRANGE mylist -2 -1</c>
    /// Output: <c>["d", "e"]</c>
    ///
    /// Input:  <c>LRANGE mylist 0 -1</c>
    /// Output: <c>["a", "b", "c", "d", "e"]</c>
    ///
    /// Input:  <c>LRANGE mylist 3 1</c>
    /// Output: <c>[]</c> — start is after end, empty range
    ///
    /// Input:  <c>LRANGE mylist 0 abc</c>
    /// Output: <c>-ERR value is not an integer or out of range</c>
    /// </example>
    protected override RedisValue HandleSpecific(Command command, Socket socket)
    {
        string key = command.Arguments[0];

        // Reject non-integer index arguments before any list lookup
        if (!int.TryParse(command.Arguments[1], out int start) ||
          !int.TryParse(command.Arguments[2], out int end))
        {
            return RedisValue.ToError("ERR value is not an integer or out of range");
        }

        // Missing key is not an error in Redis — return empty array
        if (!listStorage.TryGetList(key, out IReadOnlyCollection<string>? list))
        {
            return RedisValue.EmptyBulkStringArray;
        }

        list ??= [];
        if (list.Count == 0)
        {
            return RedisValue.EmptyBulkStringArray;
        }

        int count = list.Count;

        // Resolve negative start index: -1 → last element, -2 → second-to-last, etc.
        // Clamp to 0 so a very negative start doesn't go out of bounds
        if (start < 0)
        {
            start = Math.Max(0, count + start);
        }

        // Resolve negative end index — no lower clamp needed here since
        // a negative result will cause rangeCount <= 0 and return empty below
        if (end < 0)
        {
            end = count + end;
        }

        // Both indices are inclusive, so the range is end - start + 1
        int rangeCount = end - start + 1;
        if (rangeCount <= 0)
        {
            return RedisValue.EmptyBulkStringArray;
        }

        return RedisValue.ToBulkStringArray(list.Skip(start).Take(rangeCount));
    }
}
