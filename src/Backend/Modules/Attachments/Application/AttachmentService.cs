using Microsoft.EntityFrameworkCore;
using Threadia.BuildingBlocks;
using Threadia.BuildingBlocks.Exceptions;
using Threadia.Modules.Attachments.Domain;
using Threadia.Modules.Attachments.Infrastructure;
using Threadia.Modules.Conversations.PublicApi;

namespace Threadia.Modules.Attachments.Application;

public sealed record UploadTicketDto(Guid AttachmentId, string UploadUrl, DateTime ExpiresAt);

public sealed record DownloadUrlDto(string Url, DateTime ExpiresAt);

public sealed class AttachmentService(
    AttachmentsDbContext db,
    IObjectStorage storage,
    IConversationMembership membership,
    TimeProvider timeProvider)
{
    private static readonly TimeSpan UploadExpiry = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan DownloadExpiry = TimeSpan.FromMinutes(5);

    /// <summary>Attachment レコードを作成し、ストレージへ直接アップロードするための署名付き URL を返す。</summary>
    public async Task<UploadTicketDto> CreateUploadAsync(
        Guid conversationId, Guid userId, string fileName, string contentType, long size, CancellationToken ct)
    {
        if (!await membership.IsActiveMemberAsync(conversationId, userId, ct))
        {
            throw new NotFoundException("会話が見つかりません。");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        Attachment attachment;
        try
        {
            attachment = Attachment.Create(Ids.New(), conversationId, userId, fileName, contentType, size, now);
        }
        catch (ArgumentException ex)
        {
            throw new ValidationException(ex.Message);
        }

        db.Attachments.Add(attachment);
        await db.SaveChangesAsync(ct);

        var uploadUrl = await storage.GetUploadUrlAsync(attachment.StorageKey, contentType, UploadExpiry, ct);
        return new UploadTicketDto(attachment.Id, uploadUrl, now.Add(UploadExpiry));
    }

    /// <summary>会話参加者だけがダウンロード URL を取得できる。</summary>
    public async Task<DownloadUrlDto> GetDownloadUrlAsync(Guid attachmentId, Guid userId, CancellationToken ct)
    {
        var attachment = await db.Attachments.AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == attachmentId, ct)
            ?? throw new NotFoundException("添付ファイルが見つかりません。");

        if (!await membership.IsActiveMemberAsync(attachment.ConversationId, userId, ct))
        {
            throw new NotFoundException("添付ファイルが見つかりません。");
        }

        // Message へ未関連付けの添付は本人以外に見せない。
        if (!attachment.IsBound && attachment.UploaderId != userId)
        {
            throw new NotFoundException("添付ファイルが見つかりません。");
        }

        var url = await storage.GetDownloadUrlAsync(attachment.StorageKey, attachment.FileName, DownloadExpiry, ct);
        return new DownloadUrlDto(url, timeProvider.GetUtcNow().UtcDateTime.Add(DownloadExpiry));
    }
}
