using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Threadia.Modules.Presence.PublicApi;

namespace Threadia.Modules.Presence.Infrastructure;

/// <summary>
/// Redis TTL ベースの Presence 実装。
/// - presence:conn:{connectionId} → userId(TTL 60秒、heartbeat で更新)
/// - presence:user:{userId} → 接続 ID の Set(TTL 120秒)
/// すべての操作は失敗してもオフライン扱いで返し、例外を伝播させない。
/// </summary>
public sealed class RedisPresenceTracker(
    Func<Task<IConnectionMultiplexer?>> connectionFactory,
    ILogger<RedisPresenceTracker> logger) : IPresenceTracker
{
    private static readonly TimeSpan ConnectionTtl = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan UserSetTtl = TimeSpan.FromSeconds(120);

    private static string ConnectionKey(string connectionId) => $"presence:conn:{connectionId}";

    private static string UserKey(Guid userId) => $"presence:user:{userId:N}";

    public Task ConnectionOpenedAsync(Guid userId, string connectionId, CancellationToken ct = default) =>
        SafeExecuteAsync(async db =>
        {
            await db.StringSetAsync(ConnectionKey(connectionId), userId.ToString("N"), ConnectionTtl);
            await db.SetAddAsync(UserKey(userId), connectionId);
            await db.KeyExpireAsync(UserKey(userId), UserSetTtl);
        });

    public Task ConnectionClosedAsync(Guid userId, string connectionId, CancellationToken ct = default) =>
        SafeExecuteAsync(async db =>
        {
            await db.KeyDeleteAsync(ConnectionKey(connectionId));
            await db.SetRemoveAsync(UserKey(userId), connectionId);
        });

    public Task HeartbeatAsync(Guid userId, string connectionId, CancellationToken ct = default) =>
        SafeExecuteAsync(async db =>
        {
            await db.StringSetAsync(ConnectionKey(connectionId), userId.ToString("N"), ConnectionTtl);
            await db.SetAddAsync(UserKey(userId), connectionId);
            await db.KeyExpireAsync(UserKey(userId), UserSetTtl);
        });

    public async Task<bool> IsOnlineAsync(Guid userId, CancellationToken ct = default)
    {
        var online = await GetOnlineUsersAsync([userId], ct);
        return online.Contains(userId);
    }

    public async Task<IReadOnlySet<Guid>> GetOnlineUsersAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
    {
        var result = new HashSet<Guid>();
        try
        {
            var connection = await connectionFactory();
            if (connection is null)
            {
                return result;
            }

            var db = connection.GetDatabase();
            foreach (var userId in userIds.Distinct())
            {
                var members = await db.SetMembersAsync(UserKey(userId));
                foreach (var member in members)
                {
                    // 接続キーが生きていればオンライン。期限切れの接続は Set から掃除する。
                    if (await db.KeyExistsAsync(ConnectionKey(member.ToString())))
                    {
                        result.Add(userId);
                        break;
                    }

                    await db.SetRemoveAsync(UserKey(userId), member);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Presence の取得に失敗しました。全員オフラインとして扱います");
        }

        return result;
    }

    private async Task SafeExecuteAsync(Func<IDatabase, Task> action)
    {
        try
        {
            var connection = await connectionFactory();
            if (connection is null)
            {
                return;
            }

            await action(connection.GetDatabase());
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Presence の更新に失敗しました(メッセージングへは影響しません)");
        }
    }
}
