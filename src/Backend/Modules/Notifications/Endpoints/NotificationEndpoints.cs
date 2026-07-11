using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Threadia.BuildingBlocks.Auth;
using Threadia.Modules.Notifications.Application;
using Threadia.Modules.Notifications.Infrastructure;

namespace Threadia.Modules.Notifications.Endpoints;

public sealed record SubscribeRequest(string Endpoint, string P256dh, string Auth);

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var me = app.MapGroup("/api/me").RequireAuthorization();

        me.MapGet("/notifications", async (int? limit, ICurrentUser currentUser, NotificationService service, CancellationToken ct) =>
            Results.Ok(await service.GetMineAsync(currentUser.UserId, limit, ct)));

        me.MapPost("/notifications/{notificationId:guid}/read",
            async (Guid notificationId, ICurrentUser currentUser, NotificationService service, CancellationToken ct) =>
            {
                await service.MarkReadAsync(notificationId, currentUser.UserId, ct);
                return Results.NoContent();
            });

        me.MapPost("/push-subscriptions", async (SubscribeRequest request, ICurrentUser currentUser, PushSubscriptionService service, CancellationToken ct) =>
        {
            await service.SubscribeAsync(currentUser.UserId, request.Endpoint, request.P256dh, request.Auth, ct);
            return Results.NoContent();
        });

        me.MapDelete("/push-subscriptions", async (string endpoint, ICurrentUser currentUser, PushSubscriptionService service, CancellationToken ct) =>
        {
            await service.UnsubscribeAsync(currentUser.UserId, endpoint, ct);
            return Results.NoContent();
        });

        // フロントエンドが購読時に使う VAPID 公開鍵。未設定なら null を返す。
        app.MapGet("/api/push/vapid-public-key", (IOptions<WebPushOptions> options) =>
                Results.Ok(new { publicKey = options.Value.IsConfigured ? options.Value.VapidPublicKey : null }))
            .RequireAuthorization();
    }
}
