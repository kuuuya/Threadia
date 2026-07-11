using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Threadia.BuildingBlocks.Auth;
using Threadia.Modules.Messaging.Application;

namespace Threadia.Modules.Messaging.Endpoints;

public sealed record SendMessageRequest(string Content, string ClientMessageId, Guid[]? MentionedUserIds, Guid[]? AttachmentIds);

public sealed record EditMessageRequest(string Content);

public sealed record UpdateReadPositionRequest(long LastReadSequence);

public static class MessagingEndpoints
{
    public static void MapMessagingEndpoints(this IEndpointRouteBuilder app)
    {
        var conversationGroup = app.MapGroup("/api/conversations/{conversationId:guid}").RequireAuthorization();

        conversationGroup.MapPost("/messages", async (Guid conversationId, SendMessageRequest request, ICurrentUser currentUser, MessageService service, CancellationToken ct) =>
        {
            var message = await service.SendAsync(
                conversationId, currentUser.UserId, request.Content, request.ClientMessageId,
                request.MentionedUserIds, request.AttachmentIds, ct);
            return Results.Created($"/api/messages/{message.Id}", message);
        });

        conversationGroup.MapGet("/messages", async (Guid conversationId, long? beforeSequence, long? afterSequence, int? limit, ICurrentUser currentUser, MessageService service, CancellationToken ct) =>
            Results.Ok(await service.GetMessagesAsync(conversationId, currentUser.UserId, beforeSequence, afterSequence, limit, ct)));

        conversationGroup.MapPut("/read-position", async (Guid conversationId, UpdateReadPositionRequest request, ICurrentUser currentUser, ReadPositionService service, CancellationToken ct) =>
            Results.Ok(await service.UpdateAsync(conversationId, currentUser.UserId, request.LastReadSequence, ct)));

        var messageGroup = app.MapGroup("/api/messages/{messageId:guid}").RequireAuthorization();

        messageGroup.MapPatch("/", async (Guid messageId, EditMessageRequest request, ICurrentUser currentUser, MessageService service, CancellationToken ct) =>
            Results.Ok(await service.EditAsync(messageId, currentUser.UserId, request.Content, ct)));

        messageGroup.MapDelete("/", async (Guid messageId, ICurrentUser currentUser, MessageService service, CancellationToken ct) =>
        {
            await service.DeleteAsync(messageId, currentUser.UserId, ct);
            return Results.NoContent();
        });

        app.MapGet("/api/workspaces/{workspaceId:guid}/unread-counts",
                async (Guid workspaceId, ICurrentUser currentUser, ReadPositionService service, CancellationToken ct) =>
                    Results.Ok(await service.GetUnreadCountsAsync(workspaceId, currentUser.UserId, ct)))
            .RequireAuthorization();
    }
}
