namespace Threadia.Modules.Notifications.Domain;

/// <summary>ブラウザの Push Subscription。無効化(410 Gone)されたものは削除する。</summary>
public sealed class PushSubscription
{
    public const int MaxEndpointLength = 1000;

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Endpoint { get; private set; } = null!;
    public string P256dh { get; private set; } = null!;
    public string Auth { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }

    private PushSubscription()
    {
    }

    public static PushSubscription Create(Guid id, Guid userId, string endpoint, string p256dh, string auth, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(endpoint) || endpoint.Length > MaxEndpointLength ||
            !Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != "https")
        {
            throw new ArgumentException("Push Subscription の endpoint が不正です。", nameof(endpoint));
        }

        if (string.IsNullOrWhiteSpace(p256dh) || string.IsNullOrWhiteSpace(auth))
        {
            throw new ArgumentException("Push Subscription の鍵情報が不正です。");
        }

        return new PushSubscription
        {
            Id = id,
            UserId = userId,
            Endpoint = endpoint,
            P256dh = p256dh,
            Auth = auth,
            CreatedAt = utcNow,
        };
    }
}
