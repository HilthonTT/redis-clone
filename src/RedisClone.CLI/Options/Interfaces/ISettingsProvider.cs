namespace RedisClone.CLI.Options.Interfaces;

public interface ISettingsProvider
{
    AppSettings GetSettings();

    Task LoadSettingsAsync(CancellationToken cancellationToken = default);

    Task SaveSettingsAsync(AppSettings options, CancellationToken cancellationToken = default);
}
