namespace Threadia.Modules.Workspaces.PublicApi;

/// <summary>他モジュールへ公開するワークスペース所属確認インターフェース。</summary>
public interface IWorkspaceMembership
{
    Task<bool> IsMemberAsync(Guid workspaceId, Guid userId, CancellationToken ct = default);

    /// <summary>指定ユーザー群のうち、ワークスペースに所属していないユーザー ID を返す。</summary>
    Task<IReadOnlyList<Guid>> FindNonMembersAsync(Guid workspaceId, IReadOnlyCollection<Guid> userIds, CancellationToken ct = default);
}
