using Microsoft.EntityFrameworkCore;
using Threadia.BuildingBlocks.Exceptions;
using Threadia.Contracts.Messaging;
using Threadia.Modules.Attachments.Application;
using Threadia.Modules.Attachments.Domain;
using Threadia.Modules.Attachments.PublicApi;

namespace Threadia.Modules.Attachments.Infrastructure;

public sealed class MessageAttachments(AttachmentsDbContext db, IObjectStorage storage) : IMessageAttachments
{
    public async Task<IReadOnlyList<MessageAttachmentDto>> ValidateAttachableAsync(
        Guid conversationId, Guid senderId, IReadOnlyCollection<Guid> attachmentIds, CancellationToken ct)
    {
        var ids = attachmentIds.Distinct().ToList();
        var attachments = await db.Attachments.AsNoTracking()
            .Where(a => ids.Contains(a.Id))
            .ToListAsync(ct);

        if (attachments.Count != ids.Count)
        {
            throw new ValidationException("存在しない添付ファイルが含まれています。");
        }

        foreach (var attachment in attachments)
        {
            if (attachment.ConversationId != conversationId || attachment.UploaderId != senderId)
            {
                throw new ValidationException("この会話に添付できないファイルが含まれています。");
            }

            if (attachment.IsBound)
            {
                throw new ValidationException("既に別のメッセージへ添付されたファイルが含まれています。");
            }

            // 完了前(ストレージ未アップロード)の Attachment を Message へ関連付けない。
            var actualSize = await storage.GetObjectSizeAsync(attachment.StorageKey, ct);
            if (actualSize is null)
            {
                throw new ValidationException($"ファイル {attachment.FileName} のアップロードが完了していません。");
            }

            if (actualSize > AttachmentPolicy.MaxSizeBytes)
            {
                throw new ValidationException($"ファイル {attachment.FileName} がサイズ上限を超えています。");
            }
        }

        return attachments
            .Select(a => new MessageAttachmentDto(a.Id, a.FileName, a.ContentType, a.Size))
            .ToList();
    }

    public async Task BindToMessageAsync(Guid messageId, IReadOnlyCollection<Guid> attachmentIds, CancellationToken ct)
    {
        var attachments = await db.Attachments
            .Where(a => attachmentIds.Contains(a.Id) && a.MessageId == null)
            .ToListAsync(ct);

        foreach (var attachment in attachments)
        {
            attachment.BindToMessage(messageId);
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<MessageAttachmentDto>>> GetByMessageIdsAsync(
        IReadOnlyCollection<Guid> messageIds, CancellationToken ct)
    {
        var attachments = await db.Attachments.AsNoTracking()
            .Where(a => a.MessageId != null && messageIds.Contains(a.MessageId.Value))
            .ToListAsync(ct);

        return attachments
            .GroupBy(a => a.MessageId!.Value)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<MessageAttachmentDto>)g
                    .Select(a => new MessageAttachmentDto(a.Id, a.FileName, a.ContentType, a.Size))
                    .ToList());
    }
}
