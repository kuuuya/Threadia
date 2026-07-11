using Microsoft.EntityFrameworkCore;
using Threadia.BuildingBlocks;
using Threadia.BuildingBlocks.Exceptions;
using Threadia.Modules.Identity.PublicApi;
using Threadia.Modules.Workspaces.Domain;
using Threadia.Modules.Workspaces.Infrastructure;

namespace Threadia.Modules.Workspaces.Application;

public sealed record WorkspaceDto(Guid Id, string Name, Guid CreatedBy, DateTime CreatedAt);

public sealed record WorkspaceMemberDto(Guid UserId, string DisplayName, string Email, string Role, DateTime JoinedAt);

public sealed class WorkspaceService(
    WorkspacesDbContext db,
    IUserDirectory userDirectory,
    TimeProvider timeProvider)
{
    public async Task<WorkspaceDto> CreateAsync(Guid userId, string name, CancellationToken ct)
    {
        Workspace workspace;
        try
        {
            workspace = Workspace.Create(Ids.New(), name, userId, timeProvider.GetUtcNow().UtcDateTime);
        }
        catch (ArgumentException ex)
        {
            throw new ValidationException(ex.Message);
        }

        db.Workspaces.Add(workspace);
        db.WorkspaceMembers.Add(WorkspaceMember.Create(workspace.Id, userId, WorkspaceRole.Owner, workspace.CreatedAt));
        await db.SaveChangesAsync(ct);

        return new WorkspaceDto(workspace.Id, workspace.Name, workspace.CreatedBy, workspace.CreatedAt);
    }

    public async Task<IReadOnlyList<WorkspaceDto>> GetMineAsync(Guid userId, int? limit, CancellationToken ct)
    {
        return await db.WorkspaceMembers.AsNoTracking()
            .Where(m => m.UserId == userId)
            .Join(db.Workspaces.AsNoTracking(), m => m.WorkspaceId, w => w.Id,
                (m, w) => new WorkspaceDto(w.Id, w.Name, w.CreatedBy, w.CreatedAt))
            .OrderBy(w => w.CreatedAt)
            .Take(Paging.ClampLimit(limit))
            .ToListAsync(ct);
    }

    /// <summary>メールアドレスでユーザーを検索してメンバーに追加する。既存メンバーなら何もしない(冪等)。</summary>
    public async Task<WorkspaceMemberDto> AddMemberAsync(Guid workspaceId, Guid actorUserId, string email, CancellationToken ct)
    {
        await EnsureMemberAsync(workspaceId, actorUserId, ct);

        var user = await userDirectory.FindByEmailAsync(email, ct)
                   ?? throw new NotFoundException("指定されたメールアドレスのユーザーが見つかりません。");

        var existing = await db.WorkspaceMembers.AsNoTracking()
            .SingleOrDefaultAsync(m => m.WorkspaceId == workspaceId && m.UserId == user.Id, ct);

        if (existing is not null)
        {
            return new WorkspaceMemberDto(user.Id, user.DisplayName, user.Email, existing.Role.ToString(), existing.JoinedAt);
        }

        var member = WorkspaceMember.Create(workspaceId, user.Id, WorkspaceRole.Member, timeProvider.GetUtcNow().UtcDateTime);
        db.WorkspaceMembers.Add(member);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation })
        {
            // 並行追加された場合も冪等に成功として扱う。
        }

        return new WorkspaceMemberDto(user.Id, user.DisplayName, user.Email, member.Role.ToString(), member.JoinedAt);
    }

    public async Task<IReadOnlyList<WorkspaceMemberDto>> GetMembersAsync(Guid workspaceId, Guid actorUserId, int? limit, int? offset, CancellationToken ct)
    {
        await EnsureMemberAsync(workspaceId, actorUserId, ct);

        var members = await db.WorkspaceMembers.AsNoTracking()
            .Where(m => m.WorkspaceId == workspaceId)
            .OrderBy(m => m.JoinedAt).ThenBy(m => m.UserId)
            .Skip(offset.GetValueOrDefault())
            .Take(Paging.ClampLimit(limit))
            .ToListAsync(ct);

        var users = (await userDirectory.GetByIdsAsync(members.Select(m => m.UserId).ToList(), ct))
            .ToDictionary(u => u.Id);

        return members
            .Where(m => users.ContainsKey(m.UserId))
            .Select(m => new WorkspaceMemberDto(
                m.UserId, users[m.UserId].DisplayName, users[m.UserId].Email, m.Role.ToString(), m.JoinedAt))
            .ToList();
    }

    private async Task EnsureMemberAsync(Guid workspaceId, Guid userId, CancellationToken ct)
    {
        var isMember = await db.WorkspaceMembers.AsNoTracking()
            .AnyAsync(m => m.WorkspaceId == workspaceId && m.UserId == userId, ct);

        if (!isMember)
        {
            // 非メンバーにはワークスペースの存在自体を秘匿する。
            throw new NotFoundException("ワークスペースが見つかりません。");
        }
    }
}
