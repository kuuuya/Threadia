using Microsoft.EntityFrameworkCore;
using Threadia.Modules.Workspaces.PublicApi;

namespace Threadia.Modules.Workspaces.Infrastructure;

public sealed class WorkspaceMembership(WorkspacesDbContext db) : IWorkspaceMembership
{
    public Task<bool> IsMemberAsync(Guid workspaceId, Guid userId, CancellationToken ct = default) =>
        db.WorkspaceMembers.AsNoTracking()
            .AnyAsync(m => m.WorkspaceId == workspaceId && m.UserId == userId, ct);

    public async Task<IReadOnlyList<Guid>> FindNonMembersAsync(Guid workspaceId, IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
    {
        var members = await db.WorkspaceMembers.AsNoTracking()
            .Where(m => m.WorkspaceId == workspaceId && userIds.Contains(m.UserId))
            .Select(m => m.UserId)
            .ToListAsync(ct);

        return userIds.Except(members).ToList();
    }
}
