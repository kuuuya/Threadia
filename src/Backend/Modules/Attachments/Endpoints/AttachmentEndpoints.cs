using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Threadia.BuildingBlocks.Auth;
using Threadia.Modules.Attachments.Application;

namespace Threadia.Modules.Attachments.Endpoints;

public sealed record CreateUploadRequest(string FileName, string ContentType, long Size);

public static class AttachmentEndpoints
{
    public static void MapAttachmentEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/conversations/{conversationId:guid}/attachments",
                async (Guid conversationId, CreateUploadRequest request, ICurrentUser currentUser, AttachmentService service, CancellationToken ct) =>
                {
                    var ticket = await service.CreateUploadAsync(
                        conversationId, currentUser.UserId, request.FileName, request.ContentType, request.Size, ct);
                    return Results.Created($"/api/attachments/{ticket.AttachmentId}", ticket);
                })
            .RequireAuthorization();

        app.MapGet("/api/attachments/{attachmentId:guid}/download-url",
                async (Guid attachmentId, ICurrentUser currentUser, AttachmentService service, CancellationToken ct) =>
                    Results.Ok(await service.GetDownloadUrlAsync(attachmentId, currentUser.UserId, ct)))
            .RequireAuthorization();
    }
}
