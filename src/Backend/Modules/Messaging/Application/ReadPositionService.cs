using Microsoft.EntityFrameworkCore;
using Threadia.BuildingBlocks.Exceptions;
using Threadia.Modules.Conversations.PublicApi;
using Threadia.Modules.Messaging.Domain;
using Threadia.Modules.Messaging.Infrastructure;

namespace Threadia.Modules.Messaging.Application;

public sealed class ReadPositionService(
    MessagingDbContext db,
    IConversationMembership membership,
    TimeProvider timeProvider)
{
    /// <summary>
    /// 既読位置を更新する。LastReadSequence は後退させない(後退要求は現状維持で成功として返す)。
    /// </summary>
    public async Task<ReadPositionDto> UpdateAsync(Guid conversationId, Guid userId, long lastReadSequence, CancellationToken ct)
    {
        if (lastReadSequence < 0)
        {
            throw new ValidationException("既読位置は0以上を指定してください。");
        }

        if (!await membership.IsActiveMemberAsync(conversationId, userId, ct))
        {
            throw new NotFoundException("会話が見つかりません。");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;

        // UPSERT + GREATEST で並行更新でも後退しないことを DB レベルで保証する。
        var results = await db.Database.SqlQuery<long>($"""
            INSERT INTO messaging."ReadPositions" ("UserId", "ConversationId", "LastReadSequence", "UpdatedAt")
            VALUES ({userId}, {conversationId}, {lastReadSequence}, {now})
            ON CONFLICT ("UserId", "ConversationId")
            DO UPDATE SET
                "LastReadSequence" = GREATEST("ReadPositions"."LastReadSequence", EXCLUDED."LastReadSequence"),
                "UpdatedAt" = CASE
                    WHEN EXCLUDED."LastReadSequence" > "ReadPositions"."LastReadSequence" THEN EXCLUDED."UpdatedAt"
                    ELSE "ReadPositions"."UpdatedAt"
                END
            RETURNING "LastReadSequence" AS "Value"
            """).ToListAsync(ct);

        return new ReadPositionDto(conversationId, results.Single(), now);
    }

    /// <summary>参加中の全会話の未読数を返す。未読数は最新 Sequence と既読位置の差から計算する。</summary>
    public async Task<IReadOnlyList<UnreadCountDto>> GetUnreadCountsAsync(Guid workspaceId, Guid userId, CancellationToken ct)
    {
        var conversationIds = await membership.GetActiveConversationIdsAsync(userId, workspaceId, ct);
        if (conversationIds.Count == 0)
        {
            return [];
        }

        var latestByConversation = await db.ConversationSequences.AsNoTracking()
            .Where(s => conversationIds.Contains(s.ConversationId))
            .ToDictionaryAsync(s => s.ConversationId, s => s.LastSequence, ct);

        var readByConversation = await db.ReadPositions.AsNoTracking()
            .Where(p => p.UserId == userId && conversationIds.Contains(p.ConversationId))
            .ToDictionaryAsync(p => p.ConversationId, p => p.LastReadSequence, ct);

        return conversationIds
            .Select(id =>
            {
                var latest = latestByConversation.GetValueOrDefault(id, 0L);
                var read = Math.Min(readByConversation.GetValueOrDefault(id, 0L), latest);
                return new UnreadCountDto(id, latest, read, latest - read);
            })
            .ToList();
    }
}
