using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Threadia.BuildingBlocks.Events;

/// <summary>
/// 登録された IIntegrationEventConsumer ごとに quorum キューを作成し、メッセージを処理するホスト。
/// 処理失敗は requeue し、x-delivery-limit 超過で Dead Letter Queue へ送られる。
/// </summary>
public sealed class RabbitMqConsumerHost(
    RabbitMqConnectionProvider connectionProvider,
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqConsumerHost> logger) : BackgroundService
{
    private readonly List<IChannel> _channels = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 起動直後は RabbitMQ が未起動の場合があるためリトライする。
        IConnection connection;
        while (true)
        {
            try
            {
                connection = await connectionProvider.GetConnectionAsync(stoppingToken);
                break;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "RabbitMQ への接続に失敗しました。5秒後に再試行します");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        // Consumer 名とイベント種別は DI 登録から一時スコープで収集する(定義は不変)。
        List<(string Name, string[] EventTypes)> consumerDefinitions;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            consumerDefinitions = scope.ServiceProvider.GetServices<IIntegrationEventConsumer>()
                .Select(c => (c.Name, c.EventTypes.ToArray()))
                .ToList();
        }

        foreach (var (name, eventTypes) in consumerDefinitions)
        {
            await StartConsumerAsync(connection, name, eventTypes, stoppingToken);
        }

        logger.LogInformation("RabbitMQ consumer host started. Consumers={Consumers}",
            string.Join(",", consumerDefinitions.Select(d => d.Name)));

        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken).ContinueWith(_ => { }, CancellationToken.None);
    }

    private async Task StartConsumerAsync(IConnection connection, string consumerName, string[] eventTypes, CancellationToken ct)
    {
        var channel = await connection.CreateChannelAsync(cancellationToken: ct);
        _channels.Add(channel);

        await RabbitMqTopology.DeclareExchangesAsync(channel, options.Value, ct);
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 10, global: false, cancellationToken: ct);

        var queueName = $"threadia.{consumerName}";
        var arguments = new Dictionary<string, object?>
        {
            ["x-queue-type"] = "quorum",
            ["x-delivery-limit"] = options.Value.DeliveryLimit,
            ["x-dead-letter-exchange"] = options.Value.DeadLetterExchange,
        };
        await channel.QueueDeclareAsync(queueName, durable: true, exclusive: false, autoDelete: false, arguments, cancellationToken: ct);
        foreach (var eventType in eventTypes)
        {
            await channel.QueueBindAsync(queueName, options.Value.Exchange, routingKey: eventType, cancellationToken: ct);
        }

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            var integrationEvent = new IntegrationEvent(
                Guid.TryParse(ea.BasicProperties.MessageId, out var id) ? id : Guid.Empty,
                ea.BasicProperties.Type ?? ea.RoutingKey,
                Encoding.UTF8.GetString(ea.Body.Span),
                DateTimeOffset.FromUnixTimeSeconds(ea.BasicProperties.Timestamp.UnixTime).UtcDateTime);

            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var handler = scope.ServiceProvider.GetServices<IIntegrationEventConsumer>()
                    .First(c => c.Name == consumerName);
                await handler.HandleAsync(integrationEvent, ct);
                await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Consumer {Consumer} failed for event {EventId} ({EventType})",
                    consumerName, integrationEvent.Id, integrationEvent.Type);
                // requeue し、x-delivery-limit 超過で DLQ へ。
                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true, cancellationToken: ct);
            }
        };

        await channel.BasicConsumeAsync(queueName, autoAck: false, consumer, cancellationToken: ct);
    }

    public override void Dispose()
    {
        foreach (var channel in _channels)
        {
            channel.Dispose();
        }

        base.Dispose();
    }
}
