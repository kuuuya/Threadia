using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Threadia.BuildingBlocks.Events;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string ConnectionString { get; set; } = "amqp://guest:guest@localhost:5672/";
    public string Exchange { get; set; } = "threadia.events";
    public string DeadLetterExchange { get; set; } = "threadia.dlx";
    public string DeadLetterQueue { get; set; } = "threadia.dead";

    /// <summary>この回数を超えて再配信されたメッセージは Dead Letter Queue へ送られる。</summary>
    public int DeliveryLimit { get; set; } = 5;
}

/// <summary>RabbitMQ 接続をプロセス内で共有する。接続断は自動リカバリに任せる。</summary>
public sealed class RabbitMqConnectionProvider(IOptions<RabbitMqOptions> options) : IAsyncDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IConnection? _connection;

    public async Task<IConnection> GetConnectionAsync(CancellationToken ct)
    {
        if (_connection is { IsOpen: true })
        {
            return _connection;
        }

        await _lock.WaitAsync(ct);
        try
        {
            if (_connection is not { IsOpen: true })
            {
                var factory = new ConnectionFactory
                {
                    Uri = new Uri(options.Value.ConnectionString),
                    AutomaticRecoveryEnabled = true,
                };
                _connection = await factory.CreateConnectionAsync(ct);
            }

            return _connection;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        _lock.Dispose();
    }
}

/// <summary>
/// Outbox イベントを topic exchange へ発行する。ルーティングキーはイベント種別。
/// 発行失敗は例外として伝播し、Outbox のリトライに委ねる。
/// </summary>
public sealed class RabbitMqEventBus(
    RabbitMqConnectionProvider connectionProvider,
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqEventBus> logger) : IIntegrationEventBus, IAsyncDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IChannel? _channel;

    public async Task PublishAsync(IntegrationEvent integrationEvent, CancellationToken ct)
    {
        var channel = await GetChannelAsync(ct);

        var properties = new BasicProperties
        {
            MessageId = integrationEvent.Id.ToString(),
            Type = integrationEvent.Type,
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            Timestamp = new AmqpTimestamp(new DateTimeOffset(integrationEvent.OccurredAt).ToUnixTimeSeconds()),
        };

        await _lock.WaitAsync(ct);
        try
        {
            await channel.BasicPublishAsync(
                exchange: options.Value.Exchange,
                routingKey: integrationEvent.Type,
                mandatory: false,
                basicProperties: properties,
                body: Encoding.UTF8.GetBytes(integrationEvent.Payload),
                cancellationToken: ct);
        }
        finally
        {
            _lock.Release();
        }

        logger.LogDebug("Published {EventType} {EventId}", integrationEvent.Type, integrationEvent.Id);
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken ct)
    {
        if (_channel is { IsOpen: true })
        {
            return _channel;
        }

        await _lock.WaitAsync(ct);
        try
        {
            if (_channel is not { IsOpen: true })
            {
                var connection = await connectionProvider.GetConnectionAsync(ct);
                _channel = await connection.CreateChannelAsync(cancellationToken: ct);
                await RabbitMqTopology.DeclareExchangesAsync(_channel, options.Value, ct);
            }

            return _channel;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
        }

        _lock.Dispose();
    }
}

/// <summary>exchange / dead letter の宣言。発行側・消費側の双方から冪等に呼べる。</summary>
public static class RabbitMqTopology
{
    public static async Task DeclareExchangesAsync(IChannel channel, RabbitMqOptions options, CancellationToken ct)
    {
        await channel.ExchangeDeclareAsync(options.Exchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: ct);
        await channel.ExchangeDeclareAsync(options.DeadLetterExchange, ExchangeType.Fanout, durable: true, autoDelete: false, cancellationToken: ct);
        await channel.QueueDeclareAsync(options.DeadLetterQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
        await channel.QueueBindAsync(options.DeadLetterQueue, options.DeadLetterExchange, routingKey: string.Empty, cancellationToken: ct);
    }
}
