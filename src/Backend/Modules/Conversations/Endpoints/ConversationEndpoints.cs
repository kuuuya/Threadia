using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Threadia.BuildingBlocks.Auth;
using Threadia.Modules.Conversations.Application;

namespace Threadia.Modules.Conversations.Endpoints;

public sealed record CreateDirectRequest(Guid OtherUserId);

public sealed record CreateGroupRequest(string Name, Guid[] MemberIds);

public sealed record AddConversationMemberRequest(Guid UserId);

public static class ConversationEndpoints
{
    public static void MapConversationEndpoints(this IEndpointRouteBuilder app)
    {
        var workspaceGroup = app.MapGroup("/api/workspaces/{workspaceId:guid}/conversations").RequireAuthorization();

        workspaceGroup.MapPost("/direct", async (Guid workspaceId, CreateDirectRequest request, ICurrentUser currentUser, ConversationService service, CancellationToken ct) =>
        {
            var conversation = await service.GetOrCreateDirectAsync(workspaceId, currentUser.UserId, request.OtherUserId, ct);
            return Results.Created($"/api/conversations/{conversation.Id}", conversation);
        });

        workspaceGroup.MapPost("/group", async (Guid workspaceId, CreateGroupRequest request, ICurrentUser currentUser, ConversationService service, CancellationToken ct) =>
        {
            var conversation = await service.CreateGroupAsync(workspaceId, currentUser.UserId, request.Name, request.MemberIds, ct);
            return Results.Created($"/api/conversations/{conversation.Id}", conversation);
        });

        workspaceGroup.MapGet("/", async (Guid workspaceId, int? limit, ICurrentUser currentUser, ConversationService service, CancellationToken ct) =>
            Results.Ok(await service.GetMineAsync(workspaceId, currentUser.UserId, limit, ct)));

        var group = app.MapGroup("/api/conversations/{conversationId:guid}").RequireAuthorization();

        group.MapGet("/", async (Guid conversationId, ICurrentUser currentUser, ConversationService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(conversationId, currentUser.UserId, ct)));

        group.MapPost("/members", async (Guid conversationId, AddConversationMemberRequest request, ICurrentUser currentUser, ConversationService service, CancellationToken ct) =>
        {
            await service.AddMemberAsync(conversationId, currentUser.UserId, request.UserId, ct);
            return Results.NoContent();
        });

        group.MapPost("/leave", async (Guid conversationId, ICurrentUser currentUser, ConversationService service, CancellationToken ct) =>
        {
            await service.LeaveAsync(conversationId, currentUser.UserId, ct);
            return Results.NoContent();
        });
    }
}
