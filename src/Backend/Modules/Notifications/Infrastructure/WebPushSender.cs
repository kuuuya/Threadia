using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Threadia.Modules.Notifications.Application;
using WebPush;

namespace Threadia.Modules.Notifications.Infrastructure;

public sealed class WebPushOptions
{
    public const string SectionName = "WebPush";

    public string VapidSubject { get; set; } = "mailto:admin@example.com";
    public string VapidPublicKey { get; set; } = string.Empty;
    public string VapidPrivateKey { get; set; } = string.Empty;

    public bool IsConfigured => VapidPublicKey.Length > 0 && VapidPrivateKey.Length > 0;
}

public sealed class WebPushSender(IOptions<WebPushOptions> options, ILogger<WebPushSender> logger) : IWebPushSender
{
    private readonly WebPushClient _client = new();

    public async Task<PushSendResult> SendAsync(Domain.PushSubscription subscription, string payloadJson, CancellationToken ct)
    {
        var vapid = new VapidDetails(options.Value.VapidSubject, options.Value.VapidPublicKey, options.Value.VapidPrivateKey);
        var target = new PushSubscription(subscription.Endpoint, subscription.P256dh, subscription.Auth);

        try
        {
            await _client.SendNotificationAsync(target, payloadJson, vapid, ct);
            return PushSendResult.Ok;
        }
        catch (WebPushException ex) when (
            ex.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.Gone)
        {
            return PushSendResult.Gone;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // エンドポイント URL は個人情報に準ずるためログへ出さない。
            logger.LogWarning(ex, "Web Push の送信に失敗しました。SubscriptionId={SubscriptionId}", subscription.Id);
            return PushSendResult.Failed;
        }
    }
}

/// <summary>VAPID 鍵未設定の環境用。送信せず成功として扱う。</summary>
public sealed class NullWebPushSender(ILogger<NullWebPushSender> logger) : IWebPushSender
{
    public Task<PushSendResult> SendAsync(Domain.PushSubscription subscription, string payloadJson, CancellationToken ct)
    {
        logger.LogDebug("WebPush 未設定のため送信をスキップしました");
        return Task.FromResult(PushSendResult.Ok);
    }
}
