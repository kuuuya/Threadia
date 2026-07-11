namespace Threadia.BuildingBlocks.Events;

/// <summary>
/// モジュール間・プロセス間で交換するイベント。Id は Outbox 行の Id であり、
/// Consumer は (ConsumerName, EventId) を冪等性キーとして重複処理を防ぐ。
/// </summary>
public sealed record IntegrationEvent(Guid Id, string Type, string Payload, DateTime OccurredAt);

/// <summary>イベント発行(Outbox Worker から呼ばれる)。at-least-once 配信。</summary>
public interface IIntegrationEventBus
{
    Task PublishAsync(IntegrationEvent integrationEvent, CancellationToken ct);
}

/// <summary>
/// イベント消費者。複数回配信される前提で冪等に実装すること。
/// </summary>
public interface IIntegrationEventConsumer
{
    /// <summary>キュー名(threadia.{Name})と冪等性キーに使用する一意な名前。</summary>
    string Name { get; }

    /// <summary>購読するイベント種別(RabbitMQ ではルーティングキー)。</summary>
    IReadOnlyCollection<string> EventTypes { get; }

    Task HandleAsync(IntegrationEvent integrationEvent, CancellationToken ct);
}
