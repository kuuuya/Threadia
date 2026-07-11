using Threadia.Modules.Notifications.Domain;

namespace Threadia.Modules.Notifications.Application;

public enum PushSendResult
{
    Ok,

    /// <summary>Subscription が無効(404/410)。削除すべき。</summary>
    Gone,

    /// <summary>一時的な失敗。ログに残すのみで通知処理は継続する。</summary>
    Failed,
}

public interface IWebPushSender
{
    Task<PushSendResult> SendAsync(PushSubscription subscription, string payloadJson, CancellationToken ct);
}
