using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Threadia.Modules.Conversations.PublicApi;
using Threadia.Modules.Presence.PublicApi;

namespace Threadia.Modules.Messaging.Endpoints;

/// <summary>
/// リアルタイム配信用 Hub。SignalR は通知手段でありデータの正本ではない。
/// クライアントは受信した Sequence に欠番があれば REST API から不足分を取得する。
/// Presence の更新は失敗してもメッセージングへ影響させない(IPresenceTracker は例外を送出しない)。
/// </summary>
[Authorize]
public sealed class ChatHub(IConversationMembership membership, IPresenceTracker presence) : Hub
{
    public static string ConversationGroup(Guid conversationId) => $"conversation:{conversationId}";

    public override async Task OnConnectedAsync()
    {
        if (TryGetUserId(out var userId))
        {
            await presence.ConnectionOpenedAsync(userId, Context.ConnectionId, Context.ConnectionAborted);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (TryGetUserId(out var userId))
        {
            await presence.ConnectionClosedAsync(userId, Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>クライアントは定期的に呼び、Presence の TTL を更新する。</summary>
    public Task Heartbeat() =>
        TryGetUserId(out var userId)
            ? presence.HeartbeatAsync(userId, Context.ConnectionId, Context.ConnectionAborted)
            : Task.CompletedTask;

    /// <summary>会話の配信グループへ参加する。所属確認はサーバー側で行う。</summary>
    public async Task JoinConversation(Guid conversationId)
    {
        var userId = GetUserId();
        if (!await membership.IsActiveMemberAsync(conversationId, userId, Context.ConnectionAborted))
        {
            throw new HubException("会話が見つかりません。");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, ConversationGroup(conversationId), Context.ConnectionAborted);
    }

    public Task LeaveConversation(Guid conversationId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, ConversationGroup(conversationId), Context.ConnectionAborted);

    private bool TryGetUserId(out Guid userId)
    {
        userId = default;
        return Context.UserIdentifier is not null && Guid.TryParse(Context.UserIdentifier, out userId);
    }

    private Guid GetUserId()
    {
        if (!TryGetUserId(out var userId))
        {
            throw new HubException("認証されていません。");
        }

        return userId;
    }
}
