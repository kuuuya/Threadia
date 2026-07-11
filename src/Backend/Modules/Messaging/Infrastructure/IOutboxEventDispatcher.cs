namespace Threadia.Modules.Messaging.Infrastructure;

/// <summary>Outbox イベントの配信先。現状は SignalR のみ。通知・検索インデックス更新も将来ここに接続する。</summary>
public interface IOutboxEventDispatcher
{
    Task DispatchAsync(string type, string payload, CancellationToken ct);
}
