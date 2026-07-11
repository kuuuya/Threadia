namespace Threadia.Modules.Workspaces.Domain;

public enum WorkspaceRole
{
    Member = 0,
    Owner = 1,
}

public sealed class WorkspaceMember
{
    public Guid WorkspaceId { get; private set; }
    public Guid UserId { get; private set; }
    public WorkspaceRole Role { get; private set; }
    public DateTime JoinedAt { get; private set; }

    private WorkspaceMember()
    {
    }

    public static WorkspaceMember Create(Guid workspaceId, Guid userId, WorkspaceRole role, DateTime utcNow) =>
        new()
        {
            WorkspaceId = workspaceId,
            UserId = userId,
            Role = role,
            JoinedAt = utcNow,
        };
}
