using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Threadia.BuildingBlocks.Auth;
using Threadia.Modules.Workspaces.Application;

namespace Threadia.Modules.Workspaces.Endpoints;

public sealed record CreateWorkspaceRequest(string Name);

public sealed record AddMemberRequest(string Email);

public static class WorkspaceEndpoints
{
    public static void MapWorkspaceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/workspaces").RequireAuthorization();

        group.MapPost("/", async (CreateWorkspaceRequest request, ICurrentUser currentUser, WorkspaceService service, CancellationToken ct) =>
        {
            var workspace = await service.CreateAsync(currentUser.UserId, request.Name, ct);
            return Results.Created($"/api/workspaces/{workspace.Id}", workspace);
        });

        group.MapGet("/", async (int? limit, ICurrentUser currentUser, WorkspaceService service, CancellationToken ct) =>
            Results.Ok(await service.GetMineAsync(currentUser.UserId, limit, ct)));

        group.MapPost("/{workspaceId:guid}/members", async (Guid workspaceId, AddMemberRequest request, ICurrentUser currentUser, WorkspaceService service, CancellationToken ct) =>
        {
            var member = await service.AddMemberAsync(workspaceId, currentUser.UserId, request.Email, ct);
            return Results.Created($"/api/workspaces/{workspaceId}/members/{member.UserId}", member);
        });

        group.MapGet("/{workspaceId:guid}/members", async (Guid workspaceId, int? limit, int? offset, ICurrentUser currentUser, WorkspaceService service, CancellationToken ct) =>
            Results.Ok(await service.GetMembersAsync(workspaceId, currentUser.UserId, limit, offset, ct)));
    }
}
