namespace RedisClone.CLI.Protocol;

/// <summary>
/// Parsed RESP value from the wire. Covers all RESP types.
/// </summary>
internal sealed class RespResult
{
    public RespResultType Type { get; }

    public string? Text { get; }

    public long IntegerValue { get; }

    public RespResult[]? Elements { get; set; }

    private RespResult(RespResultType type, string? text = null, long integer = 0, RespResult[]? elements = null)
    {
        Type = type;
        Text = text;
        IntegerValue = integer;
        Elements = elements;
    }

    public static RespResult SimpleString(string text) => new(RespResultType.SimpleString, text: text);

    public static RespResult Error(string text) => new(RespResultType.Error, text: text);

    public static RespResult Integer(long value) => new(RespResultType.Integer, integer: value);

    public static RespResult BulkString(string text) => new(RespResultType.BulkString, text: text);

    public static RespResult Array(RespResult[] elements) => new(RespResultType.Array, elements: elements);

    public static RespResult Null() => new(RespResultType.Null);

    /// <summary>
    /// Returns the command name and arguments for a RESP array command.
    /// </summary>
    public (string Name, string[] Arguments) ToCommand()
    {
        if (Type != RespResultType.Array || Elements is null || Elements.Length == 0)
        {
            return ("", []);
        }

        string name = Elements[0].Text ?? "";
        string[] args = new string[Elements.Length - 1];
        for (int i = 1; i < Elements.Length; i++)
        {
            args[i - 1] = Elements[i].Text ?? "";
        }

        return (name, args);
    }
}
