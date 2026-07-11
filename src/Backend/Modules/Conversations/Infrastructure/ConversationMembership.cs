using Microsoft.EntityFrameworkCore;
using Threadia.Modules.Conversations.PublicApi;

namespace Threadia.Modules.Conversations.Infrastructure;

public sealed class ConversationMembership(ConversationsDbContext db) : IConversationMembership
{
    public Task<bool> IsActiveMemberAsync(Guid conversationId, Guid userId, CancellationToken ct = default) =>
        db.ConversationMembers.AsNoTracking()
            .AnyAsync(m => m.ConversationId == conversationId && m.UserId == userId && m.LeftAt == null, ct);

    public async Task<IReadOnlyList<Guid>> GetActiveMemberIdsAsync(Guid conversationId, CancellationToken ct = default) =>
        await db.ConversationMembers.AsNoTracking()
            .Where(m => m.ConversationId == conversationId && m.LeftAt == null)
            .Select(m => m.UserId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> GetActiveConversationIdsAsync(Guid userId, Guid workspaceId, CancellationToken ct = default) =>
        await db.ConversationMembers.AsNoTracking()
            .Where(m => m.UserId == userId && m.LeftAt == null)
            .Join(db.Conversations.AsNoTracking().Where(c => c.WorkspaceId == workspaceId),
                m => m.ConversationId, c => c.Id, (m, c) => c.Id)
            .ToListAsync(ct);
}
