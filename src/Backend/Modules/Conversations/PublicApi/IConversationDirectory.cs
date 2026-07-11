namespace Threadia.Modules.Conversations.PublicApi;

public sealed record ConversationSummary(Guid Id, Guid WorkspaceId, string Type, string? Name);

/// <summary>他モジュール(Notifications / Search)へ公開する会話情報の参照。</summary>
public interface IConversationDirectory
{
    Task<ConversationSummary?> GetSummaryAsync(Guid conversationId, CancellationToken ct = default);
}
