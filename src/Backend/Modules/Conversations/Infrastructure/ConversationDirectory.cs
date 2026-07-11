using Microsoft.EntityFrameworkCore;
using Threadia.Modules.Conversations.PublicApi;

namespace Threadia.Modules.Conversations.Infrastructure;

public sealed class ConversationDirectory(ConversationsDbContext db) : IConversationDirectory
{
    public Task<ConversationSummary?> GetSummaryAsync(Guid conversationId, CancellationToken ct = default) =>
        db.Conversations.AsNoTracking()
            .Where(c => c.Id == conversationId)
            .Select(c => new ConversationSummary(c.Id, c.WorkspaceId, c.Type.ToString(), c.Name))
            .SingleOrDefaultAsync(ct);
}
