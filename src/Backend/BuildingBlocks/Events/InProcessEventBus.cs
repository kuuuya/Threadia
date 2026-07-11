using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Threadia.BuildingBlocks.Events;

/// <summary>
/// 同一プロセス内の Consumer を直接呼び出すバス。開発(RabbitMQ なし)とテストで使用する。
/// Consumer の例外はそのまま伝播させ、Outbox のリトライに委ねる。
/// </summary>
public sealed class InProcessEventBus(IServiceScopeFactory scopeFactory, ILogger<InProcessEventBus> logger) : IIntegrationEventBus
{
    public async Task PublishAsync(IntegrationEvent integrationEvent, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var consumers = scope.ServiceProvider.GetServices<IIntegrationEventConsumer>()
            .Where(c => c.EventTypes.Contains(integrationEvent.Type));

        foreach (var consumer in consumers)
        {
            logger.LogDebug("Dispatching {EventType} to {Consumer}", integrationEvent.Type, consumer.Name);
            await consumer.HandleAsync(integrationEvent, ct);
        }
    }
}
