namespace Threadia.Modules.Presence.PublicApi;

/// <summary>
/// オンライン状態の追跡。Presence は一時情報であり、正本は Redis の TTL 付きキーのみ。
/// 実装は決して例外を送出しない(Presence 障害がメッセージ送受信へ影響してはならない)。
/// </summary>
public interface IPresenceTracker
{
    /// <summary>接続確立時に呼ぶ。複数端末の接続は connectionId ごとに個別管理する。</summary>
    Task ConnectionOpenedAsync(Guid userId, string connectionId, CancellationToken ct = default);

    Task ConnectionClosedAsync(Guid userId, string connectionId, CancellationToken ct = default);

    /// <summary>heartbeat で TTL を更新する。切断イベントだけに依存しない。</summary>
    Task HeartbeatAsync(Guid userId, string connectionId, CancellationToken ct = default);

    /// <summary>1つ以上の有効な接続があれば Online。</summary>
    Task<bool> IsOnlineAsync(Guid userId, CancellationToken ct = default);

    /// <summary>指定ユーザーのうちオンラインのユーザー ID を返す。</summary>
    Task<IReadOnlySet<Guid>> GetOnlineUsersAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default);
}
