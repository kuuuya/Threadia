namespace Threadia.Modules.Conversations.PublicApi;

/// <summary>
/// 他モジュール(Messaging など)へ公開する会話所属確認インターフェース。
/// Message の参照・投稿権限の判定に使用する。
/// </summary>
public interface IConversationMembership
{
    Task<bool> IsActiveMemberAsync(Guid conversationId, Guid userId, CancellationToken ct = default);

    Task<IReadOnlyList<Guid>> GetActiveMemberIdsAsync(Guid conversationId, CancellationToken ct = default);

    /// <summary>指定ユーザーが参加中の会話 ID を返す(未読数の集計などに使用)。</summary>
    Task<IReadOnlyList<Guid>> GetActiveConversationIdsAsync(Guid userId, Guid workspaceId, CancellationToken ct = default);
}
