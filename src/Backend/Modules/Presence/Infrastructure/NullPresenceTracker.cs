using Threadia.Modules.Presence.PublicApi;

namespace Threadia.Modules.Presence.Infrastructure;

/// <summary>Redis 未設定の環境(テストなど)用。全員オフラインとして扱う。</summary>
public sealed class NullPresenceTracker : IPresenceTracker
{
    public Task ConnectionOpenedAsync(Guid userId, string connectionId, CancellationToken ct = default) => Task.CompletedTask;

    public Task ConnectionClosedAsync(Guid userId, string connectionId, CancellationToken ct = default) => Task.CompletedTask;

    public Task HeartbeatAsync(Guid userId, string connectionId, CancellationToken ct = default) => Task.CompletedTask;

    public Task<bool> IsOnlineAsync(Guid userId, CancellationToken ct = default) => Task.FromResult(false);

    public Task<IReadOnlySet<Guid>> GetOnlineUsersAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>());
}
