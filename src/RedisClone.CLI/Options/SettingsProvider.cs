using RedisClone.CLI.Options.Interfaces;
using System.Text.Json;

namespace RedisClone.CLI.Options;

internal sealed class SettingsProvider : ISettingsProvider
{
    private AppSettings? _settings;

    public AppSettings GetSettings()
    {
        ArgumentNullException.ThrowIfNull(_settings, nameof(_settings));
        return _settings;
    }

    public async Task LoadSettingsAsync(CancellationToken cancellationToken = default)
    {
        if (_settings is not null) return;

        try
        {
            string filePath = EnsureSettingsFilePath();
            Console.WriteLine($"Settings file: {filePath}");

            await using var stream = File.OpenRead(filePath);
            _settings = await JsonSerializer.DeserializeAsync<AppSettings>(
                stream, cancellationToken: cancellationToken)
                ?? AppSettings.Default;
        }
        catch (Exception)
        {
            _settings = AppSettings.Default;
        }
    }

    public async Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _settings = settings;

        string filePath = EnsureSettingsFilePath();
        await using var stream = File.OpenWrite(filePath);
        await JsonSerializer.SerializeAsync(stream, _settings, cancellationToken: cancellationToken);
    }

    private static string EnsureSettingsFilePath()
    {
        string appDir = AppSettings.GetAppDataDirectory();

        if (!Directory.Exists(appDir))
            Directory.CreateDirectory(appDir);

        string filePath = Path.Combine(appDir, "redis-settings.json");

        if (!File.Exists(filePath))
            File.WriteAllText(filePath, "{}");

        return filePath;
    }
}