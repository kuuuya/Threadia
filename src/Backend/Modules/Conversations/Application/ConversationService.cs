using Microsoft.EntityFrameworkCore;
using Threadia.BuildingBlocks;
using Threadia.BuildingBlocks.Exceptions;
using Threadia.Modules.Conversations.Domain;
using Threadia.Modules.Conversations.Infrastructure;
using Threadia.Modules.Workspaces.PublicApi;

namespace Threadia.Modules.Conversations.Application;

public sealed record ConversationDto(
    Guid Id,
    Guid WorkspaceId,
    string Type,
    string? Name,
    Guid CreatedBy,
    DateTime CreatedAt,
    IReadOnlyList<Guid> MemberIds);

public sealed class ConversationService(
    ConversationsDbContext db,
    IWorkspaceMembership workspaceMembership,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Direct Conversation を取得または作成する。既存があればそれを返す(冪等)。
    /// </summary>
    public async Task<ConversationDto> GetOrCreateDirectAsync(Guid workspaceId, Guid actorUserId, Guid otherUserId, CancellationToken ct)
    {
        await EnsureWorkspaceMembersAsync(workspaceId, [actorUserId, otherUserId], ct);

        if (actorUserId == otherUserId)
        {
            throw new ValidationException("自分自身との Direct Conversation は作成できません。");
        }

        var directKey = Conversation.BuildDirectKey(actorUserId, otherUserId);
        var existing = await FindDirectAsync(workspaceId, directKey, ct);
        if (existing is not null)
        {
            return existing;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var conversation = Conversation.CreateDirect(Ids.New(), workspaceId, actorUserId, otherUserId, now);
        db.Conversations.Add(conversation);
        db.ConversationMembers.Add(ConversationMember.Create(conversation.Id, actorUserId, now));
        db.ConversationMembers.Add(ConversationMember.Create(conversation.Id, otherUserId, now));

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation })
        {
            // 並行作成された場合は既存の会話を返す。
            db.ChangeTracker.Clear();
            return await FindDirectAsync(workspaceId, directKey, ct)
                   ?? throw new ConflictException("Direct Conversation の作成が競合しました。再試行してください。");
        }

        return ToDto(conversation, [actorUserId, otherUserId]);
    }

    public async Task<ConversationDto> CreateGroupAsync(Guid workspaceId, Guid actorUserId, string name, IReadOnlyCollection<Guid> memberIds, CancellationToken ct)
    {
        var allMemberIds = memberIds.Append(actorUserId).Distinct().ToList();
        await EnsureWorkspaceMembersAsync(workspaceId, allMemberIds, ct);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        Conversation conversation;
        try
        {
            conversation = Conversation.CreateGroup(Ids.New(), workspaceId, name, actorUserId, now);
        }
        catch (ArgumentException ex)
        {
            throw new ValidationException(ex.Message);
        }

        db.Conversations.Add(conversation);
        foreach (var memberId in allMemberIds)
        {
            db.ConversationMembers.Add(ConversationMember.Create(conversation.Id, memberId, now));
        }

        await db.SaveChangesAsync(ct);
        return ToDto(conversation, allMemberIds);
    }

    public async Task<IReadOnlyList<ConversationDto>> GetMineAsync(Guid workspaceId, Guid userId, int? limit, CancellationToken ct)
    {
        if (!await workspaceMembership.IsMemberAsync(workspaceId, userId, ct))
        {
            throw new NotFoundException("ワークスペースが見つかりません。");
        }

        var conversations = await db.ConversationMembers.AsNoTracking()
            .Where(m => m.UserId == userId && m.LeftAt == null)
            .Join(db.Conversations.AsNoTracking().Where(c => c.WorkspaceId == workspaceId),
                m => m.ConversationId, c => c.Id, (m, c) => c)
            .OrderBy(c => c.CreatedAt)
            .Take(Paging.ClampLimit(limit))
            .ToListAsync(ct);

        var conversationIds = conversations.Select(c => c.Id).ToList();
        var members = await db.ConversationMembers.AsNoTracking()
            .Where(m => conversationIds.Contains(m.ConversationId) && m.LeftAt == null)
            .ToListAsync(ct);

        var membersByConversation = members.ToLookup(m => m.ConversationId, m => m.UserId);
        return conversations
            .Select(c => ToDto(c, membersByConversation[c.Id].ToList()))
            .ToList();
    }

    public async Task<ConversationDto> GetAsync(Guid conversationId, Guid userId, CancellationToken ct)
    {
        var conversation = await GetAccessibleConversationAsync(conversationId, userId, ct);
        var memberIds = await db.ConversationMembers.AsNoTracking()
            .Where(m => m.ConversationId == conversationId && m.LeftAt == null)
            .Select(m => m.UserId)
            .ToListAsync(ct);

        return ToDto(conversation, memberIds);
    }

    /// <summary>Group への参加者追加。退出済みメンバーの再追加も可能(冪等)。</summary>
    public async Task AddMemberAsync(Guid conversationId, Guid actorUserId, Guid userId, CancellationToken ct)
    {
        var conversation = await GetAccessibleConversationAsync(conversationId, actorUserId, ct);
        if (conversation.Type != ConversationType.Group)
        {
            throw new ValidationException("Direct Conversation には参加者を追加できません。");
        }

        await EnsureWorkspaceMembersAsync(conversation.WorkspaceId, [userId], ct);

        var existing = await db.ConversationMembers
            .SingleOrDefaultAsync(m => m.ConversationId == conversationId && m.UserId == userId, ct);

        if (existing is null)
        {
            db.ConversationMembers.Add(ConversationMember.Create(conversationId, userId, timeProvider.GetUtcNow().UtcDateTime));
        }
        else
        {
            existing.Rejoin();
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation })
        {
            // 並行追加された場合も成功として扱う。
        }
    }

    public async Task LeaveAsync(Guid conversationId, Guid userId, CancellationToken ct)
    {
        var conversation = await GetAccessibleConversationAsync(conversationId, userId, ct);
        if (conversation.Type != ConversationType.Group)
        {
            throw new ValidationException("Direct Conversation からは退出できません。");
        }

        var member = await db.ConversationMembers
            .SingleAsync(m => m.ConversationId == conversationId && m.UserId == userId, ct);

        member.Leave(timeProvider.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(ct);
    }

    private async Task<ConversationDto?> FindDirectAsync(Guid workspaceId, string directKey, CancellationToken ct)
    {
        var conversation = await db.Conversations.AsNoTracking()
            .SingleOrDefaultAsync(c => c.WorkspaceId == workspaceId && c.DirectKey == directKey, ct);

        if (conversation is null)
        {
            return null;
        }

        var memberIds = await db.ConversationMembers.AsNoTracking()
            .Where(m => m.ConversationId == conversation.Id && m.LeftAt == null)
            .Select(m => m.UserId)
            .ToListAsync(ct);

        return ToDto(conversation, memberIds);
    }

    private async Task<Conversation> GetAccessibleConversationAsync(Guid conversationId, Guid userId, CancellationToken ct)
    {
        var isMember = await db.ConversationMembers.AsNoTracking()
            .AnyAsync(m => m.ConversationId == conversationId && m.UserId == userId && m.LeftAt == null, ct);

        if (!isMember)
        {
            // 非参加者には会話の存在自体を秘匿する。
            throw new NotFoundException("会話が見つかりません。");
        }

        return await db.Conversations.SingleAsync(c => c.Id == conversationId, ct);
    }

    private async Task EnsureWorkspaceMembersAsync(Guid workspaceId, IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        var nonMembers = await workspaceMembership.FindNonMembersAsync(workspaceId, userIds, ct);
        if (nonMembers.Count > 0)
        {
            throw new ValidationException("ワークスペースに所属していないユーザーが含まれています。");
        }
    }

    private static ConversationDto ToDto(Conversation conversation, IReadOnlyList<Guid> memberIds) =>
        new(conversation.Id, conversation.WorkspaceId, conversation.Type.ToString(), conversation.Name,
            conversation.CreatedBy, conversation.CreatedAt, memberIds);
}
