namespace Threadia.Modules.Conversations.Domain;

public enum ConversationType
{
    Direct = 0,
    Group = 1,
}

public sealed class Conversation
{
    public const int MaxNameLength = 80;

    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public ConversationType Type { get; private set; }

    /// <summary>Group のみ。Direct は相手の表示名をクライアント側で表示する。</summary>
    public string? Name { get; private set; }

    /// <summary>Direct の重複作成を防ぐキー。2ユーザー ID を昇順に並べた文字列。Group では null。</summary>
    public string? DirectKey { get; private set; }

    public Guid CreatedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Conversation()
    {
    }

    public static Conversation CreateDirect(Guid id, Guid workspaceId, Guid userA, Guid userB, DateTime utcNow)
    {
        if (userA == userB)
        {
            throw new ArgumentException("自分自身との Direct Conversation は作成できません。", nameof(userB));
        }

        return new Conversation
        {
            Id = id,
            WorkspaceId = workspaceId,
            Type = ConversationType.Direct,
            DirectKey = BuildDirectKey(userA, userB),
            CreatedBy = userA,
            CreatedAt = utcNow,
        };
    }

    public static Conversation CreateGroup(Guid id, Guid workspaceId, string name, Guid createdBy, DateTime utcNow)
    {
        var trimmed = name.Trim();
        if (trimmed.Length is 0 or > MaxNameLength)
        {
            throw new ArgumentException($"会話名は1〜{MaxNameLength}文字で指定してください。", nameof(name));
        }

        return new Conversation
        {
            Id = id,
            WorkspaceId = workspaceId,
            Type = ConversationType.Group,
            Name = trimmed,
            CreatedBy = createdBy,
            CreatedAt = utcNow,
        };
    }

    /// <summary>ユーザー ID の組に対して常に同じキーを返す(順序に依存しない)。</summary>
    public static string BuildDirectKey(Guid userA, Guid userB)
    {
        var (first, second) = userA.CompareTo(userB) < 0 ? (userA, userB) : (userB, userA);
        return $"{first:N}:{second:N}";
    }
}
