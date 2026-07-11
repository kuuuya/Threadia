using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Threadia.BuildingBlocks;
using Threadia.BuildingBlocks.Exceptions;
using Threadia.Contracts.Messaging;
using Threadia.Modules.Attachments.PublicApi;
using Threadia.Modules.Conversations.PublicApi;
using Threadia.Modules.Messaging.Domain;
using Threadia.Modules.Messaging.Infrastructure;

namespace Threadia.Modules.Messaging.Application;

public sealed class MessageService(
    MessagingDbContext db,
    IConversationMembership membership,
    IMessageAttachments messageAttachments,
    TimeProvider timeProvider)
{
    /// <summary>
    /// メッセージを送信する。Sequence 採番・Message 保存・Outbox 書き込みを同一トランザクションで行う。
    /// 同じ (SenderId, ClientMessageId) の再送は既存メッセージを返す(冪等)。
    /// 添付ファイルは事前検証(アップロード完了・本人・同一会話)し、コミット後に関連付ける。
    /// </summary>
    public async Task<MessageDto> SendAsync(
        Guid conversationId,
        Guid senderId,
        string content,
        string clientMessageId,
        IReadOnlyCollection<Guid>? mentionedUserIds,
        IReadOnlyCollection<Guid>? attachmentIds,
        CancellationToken ct)
    {
        await EnsureActiveMemberAsync(conversationId, senderId, ct);

        var mentions = (mentionedUserIds ?? []).Distinct().ToList();
        if (mentions.Count > 0)
        {
            var activeMembers = await membership.GetActiveMemberIdsAsync(conversationId, ct);
            if (mentions.Except(activeMembers).Any())
            {
                throw new ValidationException("会話に参加していないユーザーはメンションできません。");
            }
        }

        // 再送の場合は既存メッセージを返す。
        var existing = await FindByClientMessageIdAsync(senderId, clientMessageId, ct);
        if (existing is not null)
        {
            if (existing.ConversationId != conversationId)
            {
                throw new ConflictException("同じ ClientMessageId が別の会話で使用されています。");
            }

            return existing;
        }

        // 添付の検証(完了前の Attachment を Message へ関連付けない)。
        var attachmentIdList = (attachmentIds ?? []).Distinct().ToList();
        IReadOnlyList<MessageAttachmentDto> attachments = [];
        if (attachmentIdList.Count > 0)
        {
            attachments = await messageAttachments.ValidateAttachableAsync(conversationId, senderId, attachmentIdList, ct);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // Conversation ごとのカウンタ行を UPSERT し、行ロックで採番を直列化する。
        // ロックはコミットまで保持されるため、並行送信でも Sequence は重複しない(ADR 0001)。
        var sequences = await db.Database.SqlQuery<long>($"""
            INSERT INTO messaging."ConversationSequences" ("ConversationId", "LastSequence")
            VALUES ({conversationId}, 1)
            ON CONFLICT ("ConversationId")
            DO UPDATE SET "LastSequence" = "ConversationSequences"."LastSequence" + 1
            RETURNING "LastSequence" AS "Value"
            """).ToListAsync(ct);
        var sequence = sequences.Single();

        Message message;
        try
        {
            message = Message.Create(Ids.New(), conversationId, sequence, senderId, clientMessageId, content, now);
        }
        catch (ArgumentException ex)
        {
            throw new ValidationException(ex.Message);
        }

        db.Messages.Add(message);
        foreach (var mentionedUserId in mentions)
        {
            db.MessageMentions.Add(MessageMention.Create(message.Id, mentionedUserId));
        }

        var dto = ToDto(message, mentions, attachments);
        db.OutboxMessages.Add(OutboxMessage.Create(
            Ids.New(),
            MessagingEventTypes.MessageSent,
            JsonSerializer.Serialize(new MessageEventPayload(conversationId, dto), MessagingEventTypes.SerializerOptions),
            now));

        try
        {
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation })
        {
            // 並行再送で先に登録された場合は既存メッセージを返す。
            await tx.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            return await FindByClientMessageIdAsync(senderId, clientMessageId, ct)
                   ?? throw new ConflictException("メッセージ送信が競合しました。再試行してください。");
        }

        // 添付の関連付けはコミット後に行う(モジュール間でトランザクションを共有しない)。
        // 失敗した場合、添付は孤立ファイルとして定期掃除される。
        if (attachmentIdList.Count > 0)
        {
            await messageAttachments.BindToMessageAsync(message.Id, attachmentIdList, ct);
        }

        return dto;
    }

    /// <summary>
    /// 履歴取得。Sequence をカーソルとし、OFFSET ページングは使用しない。
    /// beforeSequence 指定(または未指定)で新しい順の選択、afterSequence 指定で欠番補完用の古い順の選択。
    /// 返却は常に Sequence 昇順。
    /// </summary>
    public async Task<MessagePageDto> GetMessagesAsync(
        Guid conversationId, Guid userId, long? beforeSequence, long? afterSequence, int? limit, CancellationToken ct)
    {
        await EnsureActiveMemberAsync(conversationId, userId, ct);

        if (beforeSequence is not null && afterSequence is not null)
        {
            throw new ValidationException("beforeSequence と afterSequence は同時に指定できません。");
        }

        var take = Paging.ClampLimit(limit);
        var query = db.Messages.AsNoTracking().Where(m => m.ConversationId == conversationId);

        List<Message> messages;
        if (afterSequence is not null)
        {
            messages = await query
                .Where(m => m.Sequence > afterSequence)
                .OrderBy(m => m.Sequence)
                .Take(take + 1)
                .ToListAsync(ct);
        }
        else
        {
            if (beforeSequence is not null)
            {
                query = query.Where(m => m.Sequence < beforeSequence);
            }

            messages = await query
                .OrderByDescending(m => m.Sequence)
                .Take(take + 1)
                .ToListAsync(ct);
            messages.Reverse();
        }

        var hasMore = messages.Count > take;
        if (hasMore)
        {
            // 新しい順の取得では古い側、欠番補完では新しい側の余剰1件を落とす。
            if (afterSequence is not null)
            {
                messages.RemoveAt(messages.Count - 1);
            }
            else
            {
                messages.RemoveAt(0);
            }
        }

        var messageIds = messages.Select(m => m.Id).ToList();
        var mentionsByMessage = (await db.MessageMentions.AsNoTracking()
                .Where(m => messageIds.Contains(m.MessageId))
                .ToListAsync(ct))
            .ToLookup(m => m.MessageId, m => m.MentionedUserId);
        var attachmentsByMessage = await messageAttachments.GetByMessageIdsAsync(messageIds, ct);

        var items = messages
            .Select(m => ToDto(
                m,
                mentionsByMessage[m.Id].ToList(),
                attachmentsByMessage.GetValueOrDefault(m.Id, [])))
            .ToList();

        return new MessagePageDto(items, hasMore);
    }

    public async Task<MessageDto> EditAsync(Guid messageId, Guid userId, string content, CancellationToken ct)
    {
        var message = await GetOwnMessageAsync(messageId, userId, ct);
        if (message.IsDeleted)
        {
            throw new NotFoundException("メッセージが見つかりません。");
        }

        try
        {
            message.Edit(content, timeProvider.GetUtcNow().UtcDateTime);
        }
        catch (ArgumentException ex)
        {
            throw new ValidationException(ex.Message);
        }

        var mentions = await db.MessageMentions.AsNoTracking()
            .Where(m => m.MessageId == messageId)
            .Select(m => m.MentionedUserId)
            .ToListAsync(ct);
        var attachments = await messageAttachments.GetByMessageIdsAsync([messageId], ct);

        var dto = ToDto(message, mentions, attachments.GetValueOrDefault(messageId, []));
        db.OutboxMessages.Add(OutboxMessage.Create(
            Ids.New(),
            MessagingEventTypes.MessageEdited,
            JsonSerializer.Serialize(new MessageEventPayload(message.ConversationId, dto), MessagingEventTypes.SerializerOptions),
            timeProvider.GetUtcNow().UtcDateTime));

        await db.SaveChangesAsync(ct);
        return dto;
    }

    public async Task DeleteAsync(Guid messageId, Guid userId, CancellationToken ct)
    {
        var message = await GetOwnMessageAsync(messageId, userId, ct);
        if (message.IsDeleted)
        {
            return;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        message.Delete(now);
        db.OutboxMessages.Add(OutboxMessage.Create(
            Ids.New(),
            MessagingEventTypes.MessageDeleted,
            JsonSerializer.Serialize(
                new MessageDeletedPayload(message.ConversationId, message.Id, message.Sequence),
                MessagingEventTypes.SerializerOptions),
            now));

        await db.SaveChangesAsync(ct);
    }

    private async Task<Message> GetOwnMessageAsync(Guid messageId, Guid userId, CancellationToken ct)
    {
        var message = await db.Messages.SingleOrDefaultAsync(m => m.Id == messageId, ct)
                      ?? throw new NotFoundException("メッセージが見つかりません。");

        // 非参加者にはメッセージの存在自体を秘匿する。
        if (!await membership.IsActiveMemberAsync(message.ConversationId, userId, ct))
        {
            throw new NotFoundException("メッセージが見つかりません。");
        }

        if (message.SenderId != userId)
        {
            throw new ForbiddenException("このメッセージを操作する権限がありません。");
        }

        return message;
    }

    private async Task<MessageDto?> FindByClientMessageIdAsync(Guid senderId, string clientMessageId, CancellationToken ct)
    {
        var message = await db.Messages.AsNoTracking()
            .SingleOrDefaultAsync(m => m.SenderId == senderId && m.ClientMessageId == clientMessageId, ct);

        if (message is null)
        {
            return null;
        }

        var mentions = await db.MessageMentions.AsNoTracking()
            .Where(m => m.MessageId == message.Id)
            .Select(m => m.MentionedUserId)
            .ToListAsync(ct);
        var attachments = await messageAttachments.GetByMessageIdsAsync([message.Id], ct);

        return ToDto(message, mentions, attachments.GetValueOrDefault(message.Id, []));
    }

    private async Task EnsureActiveMemberAsync(Guid conversationId, Guid userId, CancellationToken ct)
    {
        if (!await membership.IsActiveMemberAsync(conversationId, userId, ct))
        {
            throw new NotFoundException("会話が見つかりません。");
        }
    }

    private static MessageDto ToDto(
        Message message, IReadOnlyList<Guid> mentionedUserIds, IReadOnlyList<MessageAttachmentDto> attachments) =>
        new(
            message.Id,
            message.ConversationId,
            message.Sequence,
            message.SenderId,
            message.ClientMessageId,
            message.IsDeleted ? string.Empty : message.Content,
            message.CreatedAt,
            message.EditedAt,
            message.IsDeleted,
            mentionedUserIds,
            message.IsDeleted ? [] : attachments);
}
