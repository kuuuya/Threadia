using Microsoft.EntityFrameworkCore;
using Threadia.BuildingBlocks;
using Threadia.BuildingBlocks.Exceptions;
using Threadia.Modules.Conversations.PublicApi;
using Threadia.Modules.Search.Infrastructure;

namespace Threadia.Modules.Search.Application;

public sealed record SearchResultDto(
    Guid MessageId, Guid ConversationId, long Sequence, Guid SenderId, string Snippet, DateTime CreatedAt);

public sealed record SearchPageDto(IReadOnlyList<SearchResultDto> Items, bool HasMore);

public sealed class SearchService(SearchDbContext db, IConversationMembership membership)
{
    private const int SnippetLength = 120;
    private const int MaxOffset = 1000;

    /// <summary>
    /// ワークスペース内のメッセージを部分一致検索する。
    /// 参加中の会話のみを対象とし、権限外・削除済みメッセージは返さない
    /// (削除済みは Consumer がインデックスから除去する)。
    /// </summary>
    public async Task<SearchPageDto> SearchAsync(
        Guid workspaceId, Guid userId, string query, int? limit, int? offset, CancellationToken ct)
    {
        var trimmed = query.Trim();
        if (trimmed.Length is 0 or > 100)
        {
            throw new ValidationException("検索キーワードは1〜100文字で指定してください。");
        }

        var conversationIds = await membership.GetActiveConversationIdsAsync(userId, workspaceId, ct);
        if (conversationIds.Count == 0)
        {
            return new SearchPageDto([], false);
        }

        var take = Paging.ClampLimit(limit);
        var skip = Math.Clamp(offset.GetValueOrDefault(), 0, MaxOffset);
        var pattern = $"%{EscapeLike(trimmed)}%";

        var entries = await db.Entries.AsNoTracking()
            .Where(e => e.WorkspaceId == workspaceId
                        && conversationIds.Contains(e.ConversationId)
                        && EF.Functions.ILike(e.Content, pattern, "\\"))
            .OrderByDescending(e => e.CreatedAt)
            .Skip(skip)
            .Take(take + 1)
            .ToListAsync(ct);

        var hasMore = entries.Count > take;
        if (hasMore)
        {
            entries.RemoveAt(entries.Count - 1);
        }

        var items = entries
            .Select(e => new SearchResultDto(
                e.MessageId, e.ConversationId, e.Sequence, e.SenderId, BuildSnippet(e.Content, trimmed), e.CreatedAt))
            .ToList();

        return new SearchPageDto(items, hasMore);
    }

    /// <summary>ILIKE のワイルドカードをエスケープする。</summary>
    internal static string EscapeLike(string input) =>
        input.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    /// <summary>最初の一致箇所を中心に前後を切り出す。</summary>
    internal static string BuildSnippet(string content, string query)
    {
        if (content.Length <= SnippetLength)
        {
            return content;
        }

        var index = content.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            index = 0;
        }

        var start = Math.Max(0, index - SnippetLength / 4);
        var length = Math.Min(SnippetLength, content.Length - start);
        var snippet = content.Substring(start, length);

        if (start > 0)
        {
            snippet = "…" + snippet;
        }

        if (start + length < content.Length)
        {
            snippet += "…";
        }

        return snippet;
    }
}
