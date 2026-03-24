namespace RedisClone.Client.Protocol;

/// <summary>
/// Represents a single value in the Redis Serialization Protocol (RESP).
/// Covers Simple Strings, Errors, Integers, Bulk Strings, Arrays, and Null.
/// </summary>
public sealed class RespValue
{
    public RespType Type { get; }

    public string? Text { get; set; }

    public long IntegerValue { get; set; }

    public RespValue[]? Elements { get; }

    public bool IsNull => Type is RespType.Null or RespType.NullArray;

    public bool IsError => Type == RespType.Error;

    private RespValue(RespType type, string? text = null, long integer = 0, RespValue[]? elements = null)
    {
        Type = type;
        Text = text;
        IntegerValue = integer;
        Elements = elements;
    }

    public static RespValue SimpleString(string text) => new(RespType.SimpleString, text: text);
    public static RespValue Error(string text) => new(RespType.Error, text: text);
    public static RespValue Integer(long value) => new(RespType.Integer, integer: value);
    public static RespValue BulkString(string text) => new(RespType.BulkString, text: text);
    public static RespValue Array(RespValue[] elements) => new(RespType.Array, elements: elements);
    public static RespValue Null() => new(RespType.Null);
    public static RespValue NullArray() => new(RespType.NullArray);

    /// <summary>
    /// Returns the string representation of this value.
    /// For Simple Strings and Bulk Strings, returns the text.
    /// For Integers, returns the number as a string.
    /// For Errors, returns the error message.
    /// For Null, returns null.
    /// </summary>
    public string? AsString() => Type switch
    {
        RespType.SimpleString => Text,
        RespType.BulkString => Text,
        RespType.Error => Text,
        RespType.Integer => IntegerValue.ToString(),
        RespType.Null or RespType.NullArray => null,
        RespType.Array => $"[{Elements?.Length ?? 0} elements]",
        _ => null,
    };

    /// <summary>
    /// Returns the integer value, or throws if this isn't an Integer type.
    /// </summary>
    public long AsLong() => Type == RespType.Integer
        ? IntegerValue
        : throw new InvalidOperationException($"Cannot convert {Type} to integer.");

    /// <summary>
    /// Returns the array elements, or throws if this isn't an Array type.
    /// </summary>
    public RespValue[] AsArray() => Type == RespType.Array
        ? Elements ?? []
        : throw new InvalidOperationException($"Cannot convert {Type} to array.");

    /// <summary>
    /// Returns the array elements as a list of strings (common for bulk string arrays).
    /// Null elements in the array are returned as null strings.
    /// </summary>
    public List<string?> AsStringList()
    {
        RespValue[] arr = AsArray();
        var result = new List<string?>(arr.Length);
        foreach (RespValue el in arr)
        {
            result.Add(el.AsString());
        }
        return result;
    }

    /// <summary>
    /// Throws a <see cref="Exception"/> if this value is a RESP error.
    /// Returns this value otherwise, allowing fluent chaining.
    /// </summary>
    public RespValue ThrowIfError()
    {
        if (IsError)
        {
            throw new Exception(Text ?? "Unknown Redis error");
        }
        return this;
    }

    public override string ToString() => Type switch
    {
        RespType.SimpleString => $"+{Text}",
        RespType.Error => $"-{Text}",
        RespType.Integer => $":{IntegerValue}",
        RespType.BulkString => $"${Text?.Length ?? -1} {Text}",
        RespType.Null => "(nil)",
        RespType.NullArray => "(nil array)",
        RespType.Array => $"*{Elements?.Length ?? 0}",
        _ => "(unknown)",
    };
}