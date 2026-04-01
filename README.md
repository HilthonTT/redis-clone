# 🔴 RedisClone

A Redis server implementation built from scratch in C#/.NET 8 — complete with RESP protocol parsing, key-value storage, lists, streams, pub/sub messaging, master-replica replication, RDB persistence, and a dedicated async client library.

Built as a deep-dive into distributed systems internals: binary protocol parsing, concurrent data structures, replication logs, and connection pooling.

---

## Architecture

```
┌──────────────────────────────────────────────────────────────────┐
│                        RedisClone.CLI                            │
│                         (The Server)                             │
│                                                                  │
│  ┌──────────┐  ┌───────────────┐  ┌───────────────────────────┐ │
│  │ TCP      │  │ RESP Parser   │  │ Command Handlers          │ │
│  │ Listener │──│ (wire → cmd)  │──│ GET SET LPUSH XADD ...    │ │
│  └──────────┘  └───────────────┘  └─────────┬─────────────────┘ │
│                                              │                   │
│  ┌───────────────────────────────────────────┼───────────────┐  │
│  │                    Storage Layer           │               │  │
│  │  ┌───────────┐  ┌─────────────┐  ┌────────┴────────────┐ │  │
│  │  │ KvpStorage│  │ ListStorage │  │  StreamStorage      │ │  │
│  │  │ (strings) │  │ (lists)     │  │  (append-only log)  │ │  │
│  │  └───────────┘  └─────────────┘  └─────────────────────┘ │  │
│  └───────────────────────────────────────────────────────────┘  │
│                                                                  │
│  ┌──────────────┐  ┌──────────────┐  ┌────────────────────────┐ │
│  │ PubSub       │  │ Replication  │  │ RDB Persistence        │ │
│  │ (fan-out +   │  │ (master →    │  │ (binary snapshot       │ │
│  │  work-queue) │  │  replica log)│  │  load on startup)      │ │
│  └──────────────┘  └──────────────┘  └────────────────────────┘ │
└──────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────┐
│                       RedisClone.Client                          │
│                      (C# Client Library)                         │
│                                                                  │
│  ┌──────────────┐  ┌──────────────┐  ┌────────────────────────┐ │
│  │ RedisClient  │  │ Connection   │  │ RESP Reader/Writer     │ │
│  │ (typed API)  │──│ Pool         │──│ (System.IO.Pipelines)  │ │
│  └──────────────┘  └──────────────┘  └────────────────────────┘ │
│                                                                  │
│  ┌──────────────┐  ┌──────────────────────────────────────────┐ │
│  │ PubSub       │  │ DI Extensions (Blazor / ASP.NET Core)   │ │
│  │ Subscriber   │  │ builder.Services.AddRedisClient(...)     │ │
│  └──────────────┘  └──────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────────┘
```

## Features

### Server (RedisClone.CLI)

**Data Structures**
- **Strings** — `GET`, `SET` with optional `PX` millisecond expiry, background eviction timer
- **Lists** — `LPUSH`, `RPUSH`, `LPOP`, `LLEN`, `LRANGE` (negative indices), `BLPOP` (blocking pop with timeout)
- **Streams** — `XADD` with explicit, partial-auto (`timestamp-*`), or fully-auto (`*`) entry IDs, monotonic ordering validation

**Pub/Sub**
- `SUBSCRIBE` / `UNSUBSCRIBE` / `PUBLISH` with fan-out delivery
- Work-queue semantics for `BLPOP` (first idle subscriber wins)
- Per-connection subscribed mode with dedicated broadcast loop

**Replication**
- Master-replica topology with `PSYNC` handshake and RDB transfer
- Replication log with binary-search offset lookup and sequential command propagation
- `WAIT` command — block until N replicas acknowledge a given offset
- `REPLCONF ACK` with backoff-based offset synchronization

**Persistence**
- RDB file parser supporting databases, string values, millisecond and second expiry timestamps
- Length-encoded integers (6-bit, 14-bit, 32-bit) and special integer encodings
- Automatic backup loading on server startup

**Server Infrastructure**
- Async TCP listener with concurrent client connections
- RESP protocol serialization (simple strings, bulk strings, arrays, integers, errors)
- Command validation via attributes (`[Argument]`, `[ReplicationRole]`, `[SupportedInSubscribedMode]`)
- DI-based command handler registry with `FrozenDictionary` dispatch
- Configurable via CLI args (`--port`, `--replicaof`, `--dir`, `--dbfilename`) and JSON settings

### Client Library (RedisClone.Client)

- **Streaming RESP parser** built on `System.IO.Pipelines` — handles partial TCP reads, binary-safe bulk strings, nested arrays
- **Bounded connection pool** — async semaphore-gated, lazy creation, automatic dead-connection recycling
- **Typed async API** — `GetAsync`, `SetAsync`, `LPushAsync`, `RPushAsync`, `LPopAsync`, `BLPopAsync`, `LLenAsync`, `LRangeAsync`, `XAddAsync`, `TypeAsync`, `KeysAsync`, `PublishAsync`, `ConfigGetAsync`, `EchoAsync`, `PingAsync`
- **Pub/Sub subscriber** — dedicated connection, `IAsyncEnumerable<RedisMessage>` message stream
- **Raw command execution** — `ExecuteAsync(string[])` for any command not covered by the typed API
- **DI integration** — `AddRedisClient()` extension for Blazor / ASP.NET Core with options delegate or connection string

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker](https://www.docker.com/) (optional, for containerized deployment)

### Run the Server

```bash
# Default (port 6379)
dotnet run --project src/RedisClone.CLI

# Custom port
dotnet run --project src/RedisClone.CLI -- --port 6380

# As a replica
dotnet run --project src/RedisClone.CLI -- --port 6380 --replicaof "localhost 6379"

# Custom persistence
dotnet run --project src/RedisClone.CLI -- --dir /data --dbfilename dump.rdb
```

### Use the Client Library

```csharp
using RedisClone.Client;

await using var client = new RedisClient("localhost", 6379);

// Strings
await client.SetAsync("name", "hans");
await client.SetAsync("session", "token", TimeSpan.FromMinutes(30));
string? name = await client.GetAsync("name");

// Lists
await client.RPushAsync("tasks", "build", "test", "deploy");
long length = await client.LLenAsync("tasks");
List<string?> all = await client.LRangeAsync("tasks");
string? next = await client.LPopAsync("tasks");

// Blocking pop (waits for data)
var result = await client.BLPopAsync("jobs", timeoutSeconds: 30);
if (result is not null)
    Console.WriteLine($"{result.Value.Key}: {result.Value.Value}");

// Streams
string? id = await client.XAddAsync("events", new()
{
    ["action"] = "click",
    ["page"] = "/home",
});

// Pub/Sub
await client.PublishAsync("notifications", "hello world");
await using var sub = await client.SubscribeAsync("notifications");
await foreach (var msg in sub.Messages())
    Console.WriteLine($"[{msg.Channel}] {msg.Message}");
```

### Blazor / ASP.NET Core Integration

```csharp
// Program.cs
using RedisClone.Client.DependencyInjection;

builder.Services.AddRedisClient(options =>
{
    options.Host = "localhost";
    options.Port = 6379;
    options.PoolSize = 20;
});
```

```csharp
// Inject anywhere
public class TaskService(RedisClient redis)
{
    public Task<long> AddAsync(string task) => redis.RPushAsync("tasks", task);
    public Task<List<string?>> AllAsync() => redis.LRangeAsync("tasks");
    public Task<string?> NextAsync() => redis.LPopAsync("tasks");
}
```

### Docker

```bash
docker compose up
```

```yaml
# docker-compose.yml
services:
  redisclone:
    build:
      context: .
      dockerfile: src/RedisClone.CLI/Dockerfile
    ports:
      - "6379:6379"

  webapp:
    build:
      context: .
      dockerfile: src/WebApp.Example/Dockerfile
    ports:
      - "8080:8080"
    environment:
      - Redis__Host=redisclone
    depends_on:
      - redisclone
```

## Supported Commands

| Category | Commands |
|----------|----------|
| **Strings** | `GET` `SET` (with `PX` expiry) |
| **Lists** | `LPUSH` `RPUSH` `LPOP` `BLPOP` `LLEN` `LRANGE` |
| **Streams** | `XADD` (explicit, auto-sequence, fully-auto IDs) |
| **Pub/Sub** | `SUBSCRIBE` `UNSUBSCRIBE` `PUBLISH` |
| **Keys** | `TYPE` `KEYS` |
| **Server** | `PING` `ECHO` `CONFIG GET` `INFO` `WAIT` |
| **Replication** | `PSYNC` `REPLCONF` |

## Project Structure

```
├── src/
│   ├── RedisClone.CLI/              # The Redis server
│   │   ├── Commands/
│   │   │   ├── Handlers/            # One handler per command
│   │   │   │   ├── Validation/      # [Argument], [ReplicationRole] attributes
│   │   │   │   ├── Get.cs
│   │   │   │   ├── Set.cs
│   │   │   │   ├── LPush.cs
│   │   │   │   ├── BLPop.cs
│   │   │   │   ├── XAdd.cs
│   │   │   │   ├── Subscribe.cs
│   │   │   │   └── ...
│   │   │   ├── Command.cs           # RESP parser
│   │   │   └── CommandProcessor.cs  # FrozenDictionary dispatch
│   │   ├── Storage/
│   │   │   ├── KvpStorage.cs        # ConcurrentDictionary + TTL eviction
│   │   │   ├── ListStorage.cs       # ConcurrentDictionary<LinkedList>
│   │   │   ├── StreamStorage.cs     # Append-only log with SortedSet timestamps
│   │   │   └── StorageManager.cs    # Unified key/type lookup
│   │   ├── Subscriptions/
│   │   │   └── PubSub.cs            # Fan-out + work-queue delivery
│   │   ├── Replication/
│   │   │   ├── MasterManager.cs     # Propagation loop + WAIT
│   │   │   ├── ReplicaManager.cs    # Handshake + command processing
│   │   │   └── ReplicationLog.cs    # Offset-tracked binary log
│   │   ├── Persistence/
│   │   │   └── RdbParser.cs         # Binary RDB format reader
│   │   └── Server/
│   │       ├── Server.cs            # TCP accept loop
│   │       └── TcpConnectionWorker.cs
│   │
│   ├── RedisClone.Client/           # C# client library
│   │   ├── Protocol/
│   │   │   ├── RespReader.cs        # PipeReader-based streaming parser
│   │   │   ├── RespWriter.cs        # Command serializer
│   │   │   └── RespValue.cs         # Typed RESP value union
│   │   ├── Pooling/
│   │   │   └── ConnectionPool.cs    # Bounded async pool
│   │   ├── PubSub/
│   │   │   └── RedisSubscriber.cs   # IAsyncEnumerable message stream
│   │   ├── DependencyInjection/
│   │   │   └── ServiceCollectionExtensions.cs
│   │   ├── RedisClient.cs           # Public API surface
│   │   └── RedisConnection.cs       # Single TCP + PipeReader/PipeWriter
│   │
│   └── WebApp.Example/              # ASP.NET Core test API
│       └── Endpoints/               # Minimal API endpoints per command group
│
└── tests/
    └── RedisClone.CLI.Tests/
        ├── Handlers/                 # Per-handler unit tests
        ├── Storage/                  # KvpStorage, ListStorage, StreamStorage
        ├── Subscriptions/            # PubSub fan-out, work-queue, concurrency
        ├── Replication/              # ReplicationLog offset tracking
        ├── Commands/                 # RESP parsing, CommandProcessor
        ├── Concurrency/              # Stress tests
        └── Integration/              # Multi-command workflow tests
```

## Running Tests

```bash
dotnet test
```

The test suite covers storage layers, all command handlers, RESP protocol encoding, pub/sub delivery semantics, replication log offset tracking, argument validation, concurrent access patterns, and end-to-end multi-command workflows.

## Technical Highlights

**RESP Protocol** — The client uses `System.IO.Pipelines` for zero-copy, backpressure-aware protocol parsing that correctly handles partial TCP reads and binary-safe bulk strings. The server currently uses a simpler string-split parser.

**Replication** — Full master-to-replica flow: PSYNC handshake → empty RDB transfer → continuous command propagation via an append-only replication log with binary-search offset lookup. The `WAIT` command blocks until N replicas acknowledge a target offset.

**Pub/Sub** — Two delivery modes sharing the same infrastructure: fan-out broadcast (every subscriber gets every message) for `SUBSCRIBE`, and work-queue delivery (first idle subscriber wins) for `BLPOP` notification.

**Connection Pooling** — The client pool uses `SemaphoreSlim` as a bounded async gate with lazy connection creation. Dead connections are detected on return and discarded. Pub/sub subscribers get dedicated non-pooled connections since Redis requires exclusive subscriber-mode connections.

**Command Dispatch** — Handlers are registered via DI and indexed into a `FrozenDictionary<CommandType, ICommandHandler>` at startup. Validation attributes (`[Argument]`, `[ReplicationRole]`, `[SupportedInSubscribedMode]`) are reflected once and cached in a `ConcurrentDictionary`.

## License

MIT
