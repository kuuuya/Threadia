using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Threadia.BuildingBlocks.Auth;
using Threadia.Modules.Presence.Application;

namespace Threadia.Modules.Presence.Endpoints;

public static class PresenceEndpoints
{
    public static void MapPresenceEndpoints(this IEndpointRouteBuilder app)
    {
        // 例: GET /api/workspaces/{id}/presence?userIds=...&userIds=...
        app.MapGet("/api/workspaces/{workspaceId:guid}/presence",
                async (Guid workspaceId, Guid[] userIds, ICurrentUser currentUser, PresenceService service, CancellationToken ct) =>
                    Results.Ok(await service.GetPresenceAsync(workspaceId, currentUser.UserId, userIds, ct)))
            .RequireAuthorization();
    }
}
