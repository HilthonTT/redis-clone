using RedisClone.CLI.Helpers;
using RedisClone.CLI.Options;
using RedisClone.CLI.Options.Interfaces;
using RedisClone.CLI.Persistence;
using RedisClone.CLI.Storage;

namespace RedisClone.CLI.Server;

internal sealed class ServerInitializer(
    ISettingsProvider settingsProvider,
    KvpStorage kvpStorage,
    RdbParser rdbParser)
{
    internal async Task InitializeAsync(string[] args)
    {
        AppSettings settings = settingsProvider.GetSettings();
        var kvp = ParseArgs(args);

        ApplyPortOverride(kvp, settings);
        ApplyReplicaSettings(kvp, settings);

        bool settingsChanged = false;
        settingsChanged |= ApplyDbFilenameSettings(kvp, settings);
        settingsChanged |= ApplyDirSettings(kvp, settings);

        if (settingsChanged)
        {
            await settingsProvider.SaveSettingsAsync(settings);
        }

        await LoadFromBackupFileAsync(settings.Persistence.Directory, settings.Persistence.DbFileName);
    }


    private static void ApplyPortOverride(Dictionary<string, string> kvp, AppSettings settings)
    {
        if (!kvp.TryGetValue("--port", out string? port))
        {
            return;
        }

        if (!int.TryParse(port, out int parsed) || parsed is < 1 or > 65535)
        {
            throw new ArgumentException(
               $"Invalid port value: '{port}'. Must be a number between 1 and 65535.");
        }

        settings.Runtime.Port = parsed;
    }

    private static void ApplyReplicaSettings(Dictionary<string, string> kvp, AppSettings settings)
    {
        if (kvp.TryGetValue("--replicaof", out string? replicaOf))
        {
            // Accept both "host port" (space-separated) and "host:port" formats.
            var parts = replicaOf.Contains(' ')
                ? replicaOf.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries)
                : replicaOf.Split(':', 2);

            if (parts.Length != 2 || !int.TryParse(parts[1], out int masterPort))
            {
                throw new ArgumentException(
                   $"Invalid --replicaof value: '{replicaOf}'. Expected format: '<host> <port>' or '<host>:<port>'.");
            }

            settings.Replication.Role = ReplicationRole.Slave;
            settings.Replication.SlaveReplicaSettings = new SlaveReplicaSettings
            {
                MasterHost = parts[0],
                MasterPort = masterPort,
            };

            return; // Masters don't get a replica ID.
        }

        settings.Replication.Role = ReplicationRole.Master;
        settings.Replication.MasterReplicaSettings = new MasterReplicaSettings
        {
            MasterReplicaId = StringHelpers.GenerateRandomString(40),
            MasterReplicaOffset = 0,
        };
    }

    private static bool ApplyDbFilenameSettings(Dictionary<string, string> kvp, AppSettings settings)
    {
        if (!kvp.TryGetValue("--dbfilename", out string? dbFileName))
        {
            return false;
        }
        settings.Persistence.DbFileName = dbFileName;
        return true;
    }

    private static bool ApplyDirSettings(Dictionary<string, string> kvp, AppSettings settings)
    {
        if (!kvp.TryGetValue("--dir", out string? dir))
        {
            return false;
        }
        settings.Persistence.Directory = dir;
        return true;
    }

    private async Task LoadFromBackupFileAsync(string dir, string dbFileName)
    {
        string backupFile = Path.Combine(dir, dbFileName);
        if (!File.Exists(backupFile))
        {
            return;
        }

        try
        {
            var dataModel = await rdbParser.ParseAsync(backupFile);

            // RDB files may not contain database 0 — use TryGetValue.
            if (!dataModel.Databases.TryGetValue(0, out var loadedData) || loadedData.Count == 0)
            {
                Console.WriteLine("Backup file contained no entries for database 0.");
                return;
            }

            kvpStorage.Initialize(loadedData);
            Console.WriteLine($"Loaded {loadedData.Count} records from '{backupFile}'.");
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to load backup file '{backupFile}': {e.Message}");
        }
    }

    private static Dictionary<string, string> ParseArgs(string[] args)
    {
        // Validate all flag positions upfront before applying anything.
        for (int i = 0; i < args.Length - 1; i += 2)
        {
            if (!args[i].StartsWith("--"))
            {
                throw new ArgumentException(
                   $"Expected a flag starting with '--' at position {i}, got '{args[i]}'.");
            }
        }

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < args.Length - 1; i += 2)
        {
            map.TryAdd(args[i], args[i + 1]);
        }

        return map;
    }
}
