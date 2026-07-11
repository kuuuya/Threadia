using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Threadia.BuildingBlocks.Auth;
using Threadia.Modules.Search.Application;

namespace Threadia.Modules.Search.Endpoints;

public static class SearchEndpoints
{
    public static void MapSearchEndpoints(this IEndpointRouteBuilder app)
    {
        // 例: GET /api/workspaces/{id}/search?q=hello&limit=20&offset=0
        app.MapGet("/api/workspaces/{workspaceId:guid}/search",
                async (Guid workspaceId, string q, int? limit, int? offset, ICurrentUser currentUser, SearchService service, CancellationToken ct) =>
                    Results.Ok(await service.SearchAsync(workspaceId, currentUser.UserId, q, limit, offset, ct)))
            .RequireAuthorization();
    }
}
