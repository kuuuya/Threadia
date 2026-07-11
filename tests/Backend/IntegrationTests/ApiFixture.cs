using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;
using Xunit;

namespace Threadia.IntegrationTests;

/// <summary>
/// PostgreSQL コンテナと API ホストを共有するフィクスチャ。
/// テストごとにユーザー・ワークスペースを分離するため、DB は共有してよい。
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private WebApplicationFactory<Program>? _factory;

    public IServiceProvider Services =>
        _factory?.Services ?? throw new InvalidOperationException("フィクスチャが初期化されていません。");

    public HttpClient CreateClient() =>
        _factory?.CreateClient() ?? throw new InvalidOperationException("フィクスチャが初期化されていません。");

    public async Task InitializeAsync()
    {
        if (!DockerAvailability.IsAvailable)
        {
            return;
        }

        _postgres = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("threadia_test")
            .Build();
        await _postgres.StartAsync();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
            builder.UseSetting("ConnectionStrings:Redis", string.Empty);
            builder.UseSetting("ApplyMigrationsOnStartup", "true");
            builder.UseSetting("Outbox:PollingIntervalMs", "200");
            // テストでは RabbitMQ / MinIO を起動せず、プロセス内バスとインメモリストレージを使う。
            builder.UseSetting("EventBus:Provider", "InProcess");
            builder.UseSetting("Storage:Provider", "InMemory");
        });

        // ホスト起動(マイグレーション適用)を初期化時に済ませる。
        _ = _factory.Server;
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        if (_postgres is not null)
        {
            await _postgres.DisposeAsync();
        }
    }
}

[CollectionDefinition("api")]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>;
