using FluentAssertions;
using Moq;
using RedisClone.CLI.Options;
using RedisClone.CLI.Options.Interfaces;
using RedisClone.CLI.Persistence;
using RedisClone.CLI.Server;
using RedisClone.CLI.Storage;

namespace RedisClone.CLI.Tests.Server;

public sealed class ServerInitializerTests : IDisposable
{
    private readonly Mock<ISettingsProvider> _settingsProviderMock;
    private readonly KvpStorage _kvpStorage;
    private readonly ServerInitializer _initializer;
    private readonly AppSettings _settings;

    public ServerInitializerTests()
    {
        _settings = new AppSettings
        {
            Runtime = new RuntimeSettings { Port = 6379 },
            Persistence = new PersistenceSettings
            {
                Directory = Path.GetTempPath(),
                DbFileName = "nonexistent_test.rdb"
            },
            Replication = new ReplicationSettings
            {
                Role = ReplicationRole.Master
            }
        };

        _settingsProviderMock = new Mock<ISettingsProvider>();
        _settingsProviderMock.Setup(p => p.GetSettings()).Returns(_settings);
        _settingsProviderMock
            .Setup(p => p.SaveSettingsAsync(It.IsAny<AppSettings>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _kvpStorage = new KvpStorage();

        // RdbParser is sealed — instantiate directly.
        // It won't find the nonexistent backup file, so it simply no-ops.
        var rdbParser = new RdbParser();

        _initializer = new ServerInitializer(
            _settingsProviderMock.Object,
            _kvpStorage,
            rdbParser);
    }

    [Fact]
    public async Task Initialize_NoArgs_KeepsDefaultPort()
    {
        await _initializer.InitializeAsync([]);
        _settings.Runtime.Port.Should().Be(6379);
    }

    [Fact]
    public async Task Initialize_PortArg_OverridesPort()
    {
        await _initializer.InitializeAsync(["--port", "9999"]);
        _settings.Runtime.Port.Should().Be(9999);
    }

    [Fact]
    public async Task Initialize_InvalidPort_Throws()
    {
        var act = () => _initializer.InitializeAsync(["--port", "not_a_number"]);
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Invalid port*");
    }

    [Fact]
    public async Task Initialize_PortOutOfRange_Throws()
    {
        var act = () => _initializer.InitializeAsync(["--port", "99999"]);
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Invalid port*");
    }

    [Fact]
    public async Task Initialize_PortZero_Throws()
    {
        var act = () => _initializer.InitializeAsync(["--port", "0"]);
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Invalid port*");
    }

    [Fact]
    public async Task Initialize_ReplicaOfSpaceSeparated_SetsSlaveRole()
    {
        await _initializer.InitializeAsync(["--replicaof", "localhost 6380"]);

        _settings.Replication.Role.Should().Be(ReplicationRole.Slave);
        _settings.Replication.SlaveReplicaSettings.Should().NotBeNull();
        _settings.Replication.SlaveReplicaSettings!.MasterHost.Should().Be("localhost");
        _settings.Replication.SlaveReplicaSettings!.MasterPort.Should().Be(6380);
    }

    [Fact]
    public async Task Initialize_ReplicaOfColonSeparated_SetsSlaveRole()
    {
        await _initializer.InitializeAsync(["--replicaof", "192.168.1.1:6380"]);

        _settings.Replication.Role.Should().Be(ReplicationRole.Slave);
        _settings.Replication.SlaveReplicaSettings!.MasterHost.Should().Be("192.168.1.1");
        _settings.Replication.SlaveReplicaSettings!.MasterPort.Should().Be(6380);
    }

    [Fact]
    public async Task Initialize_ReplicaOfInvalidFormat_Throws()
    {
        var act = () => _initializer.InitializeAsync(["--replicaof", "localhost"]);
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Invalid --replicaof*");
    }

    [Fact]
    public async Task Initialize_ReplicaOfNonNumericPort_Throws()
    {
        var act = () => _initializer.InitializeAsync(["--replicaof", "localhost abc"]);
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Invalid --replicaof*");
    }

    [Fact]
    public async Task Initialize_NoReplicaOf_SetsMasterRole()
    {
        await _initializer.InitializeAsync([]);

        _settings.Replication.Role.Should().Be(ReplicationRole.Master);
        _settings.Replication.MasterReplicaSettings.Should().NotBeNull();
        _settings.Replication.MasterReplicaSettings!.MasterReplicaId.Should().HaveLength(40);
        _settings.Replication.MasterReplicaSettings!.MasterReplicaOffset.Should().Be(0);
    }

    [Fact]
    public async Task Initialize_DbFilenameArg_UpdatesSettings()
    {
        await _initializer.InitializeAsync(["--dbfilename", "custom.rdb"]);

        _settings.Persistence.DbFileName.Should().Be("custom.rdb");
        _settingsProviderMock.Verify(
            p => p.SaveSettingsAsync(It.IsAny<AppSettings>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Initialize_DirArg_UpdatesSettings()
    {
        await _initializer.InitializeAsync(["--dir", "/custom/path"]);

        _settings.Persistence.Directory.Should().Be("/custom/path");
        _settingsProviderMock.Verify(
            p => p.SaveSettingsAsync(It.IsAny<AppSettings>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Initialize_NoDbFilenameOrDir_DoesNotSave()
    {
        await _initializer.InitializeAsync([]);

        _settingsProviderMock.Verify(
            p => p.SaveSettingsAsync(It.IsAny<AppSettings>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Initialize_BothDbFilenameAndDir_SavesOnce()
    {
        await _initializer.InitializeAsync(["--dir", "/path", "--dbfilename", "file.rdb"]);

        _settingsProviderMock.Verify(
            p => p.SaveSettingsAsync(It.IsAny<AppSettings>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Initialize_MultipleArgs_AllApplied()
    {
        await _initializer.InitializeAsync([
            "--port", "8080",
            "--dir", "/data",
            "--dbfilename", "dump.rdb"
        ]);

        _settings.Runtime.Port.Should().Be(8080);
        _settings.Persistence.Directory.Should().Be("/data");
        _settings.Persistence.DbFileName.Should().Be("dump.rdb");
    }

    [Fact]
    public async Task Initialize_InvalidFlagPosition_Throws()
    {
        var act = () => _initializer.InitializeAsync(["value_without_flag", "something"]);
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Expected a flag*");
    }

    [Fact]
    public async Task Initialize_CaseInsensitiveFlags()
    {
        await _initializer.InitializeAsync(["--PORT", "7777"]);
        _settings.Runtime.Port.Should().Be(7777);
    }

    [Fact]
    public async Task Initialize_DuplicateFlags_FirstOneWins()
    {
        await _initializer.InitializeAsync(["--port", "1111", "--port", "2222"]);
        _settings.Runtime.Port.Should().Be(1111);
    }

    public void Dispose() => _kvpStorage.Dispose();
}
