using RedisClone.CLI.Options;
using RedisClone.CLI.Options.Interfaces;

namespace RedisClone.CLI.Server;

/// <summary>
/// Responsible for initializing server configuration before startup,
/// loading persisted settings and applying any command-line overrides.
/// </summary>
internal sealed class ServerInitializer(ISettingsProvider settingsProvider)
{
    /// <summary>
    /// Loads settings from the settings provider and applies any overrides
    /// passed in via command-line arguments.
    /// </summary>
    /// <param name="args">
    /// Command-line arguments in flag/value pairs, e.g. <c>--port 6379 --host localhost</c>.
    /// </param>
    internal async Task InitializeAsync(string[] args)
    {
        await settingsProvider.LoadSettingsAsync();
        AppSettings settings = settingsProvider.GetSettings();
        Dictionary<string, string> kvp = ParseArgs(args);
        ApplyPortOverride(kvp, settings);
    }

    /// <summary>
    /// Overrides the configured port with the value of the <c>--port</c> flag, if present.
    /// </summary>
    /// <param name="kvp">Parsed flag/value pairs from the command line.</param>
    /// <param name="settings">The loaded application settings to mutate.</param>
    /// <exception cref="ArgumentException">
    /// Thrown if the provided port is not a valid number or falls outside the range 1–65535.
    /// </exception>
    /// <example>
    /// Input:  kvp=<c>{ "--port": "6379" }</c>
    /// Effect: settings.Runtime.Port = <c>6379</c>
    ///
    /// Input:  kvp=<c>{ "--port": "99999" }</c>
    /// Throws: <c>ArgumentException</c> — port out of range
    /// </example>
    private static void ApplyPortOverride(Dictionary<string, string> kvp, AppSettings settings)
    {
        if (!kvp.TryGetValue("--port", out string? port))
        {
            return;
        }

        if (!int.TryParse(port, out int parsedPort) || parsedPort is < 1 or > 65535)
        {
            throw new ArgumentException($"Invalid port value: '{port}'. Must be a number between 1 and 65535.");
        }

        settings.Runtime.Port = parsedPort;
    }

    /// <summary>
    /// Parses a flat array of command-line arguments into a flag/value dictionary.
    /// Arguments are expected in consecutive pairs: a flag followed by its value.
    /// Flags must begin with <c>--</c>. If duplicate flags are provided, the first wins.
    /// </summary>
    /// <param name="args">The raw command-line arguments to parse.</param>
    /// <returns>
    /// A case-insensitive dictionary mapping each flag to its value.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown if a token in a flag position does not start with <c>--</c>.
    /// </exception>
    /// <example>
    /// Input:  <c>["--port", "6379", "--host", "localhost"]</c>
    /// Output: <c>{ "--port": "6379", "--host": "localhost" }</c>
    ///
    /// Input:  <c>["--port", "6379", "--port", "9999"]</c>
    /// Output: <c>{ "--port": "6379" }</c> — first value wins
    ///
    /// Input:  <c>["--port"]</c>
    /// Output: <c>{}</c> — orphaned flag with no value is safely ignored
    /// </example>
    private static Dictionary<string, string> ParseArgs(string[] args)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Step by 2 to consume each flag/value pair together.
        // args.Length - 1 ensures we never read past the end when accessing args[i + 1].
        for (int i = 0; i < args.Length - 1; i += 2)
        {
            string key = args[i];
            string value = args[i + 1];

            if (!key.StartsWith("--"))
            {
                throw new ArgumentException($"Expected a flag starting with '--', got '{key}'.");
            }

            map.TryAdd(key, value);
        }

        return map;
    }
}
