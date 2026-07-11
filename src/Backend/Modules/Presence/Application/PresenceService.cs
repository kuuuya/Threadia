using Threadia.BuildingBlocks;
using Threadia.BuildingBlocks.Exceptions;
using Threadia.Modules.Presence.PublicApi;
using Threadia.Modules.Workspaces.PublicApi;

namespace Threadia.Modules.Presence.Application;

public sealed record UserPresenceDto(Guid UserId, bool IsOnline);

public sealed class PresenceService(IPresenceTracker tracker, IWorkspaceMembership workspaceMembership)
{
    /// <summary>同じワークスペースのメンバーだけが他メンバーのオンライン状態を参照できる。</summary>
    public async Task<IReadOnlyList<UserPresenceDto>> GetPresenceAsync(
        Guid workspaceId, Guid actorUserId, IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        if (!await workspaceMembership.IsMemberAsync(workspaceId, actorUserId, ct))
        {
            throw new NotFoundException("ワークスペースが見つかりません。");
        }

        var targets = userIds.Distinct().Take(Paging.MaxLimit).ToList();
        var nonMembers = await workspaceMembership.FindNonMembersAsync(workspaceId, targets, ct);
        targets = targets.Except(nonMembers).ToList();

        var online = await tracker.GetOnlineUsersAsync(targets, ct);
        return targets.Select(id => new UserPresenceDto(id, online.Contains(id))).ToList();
    }
}
