using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Threadia.Modules.Presence.Application;
using Threadia.Modules.Presence.Infrastructure;
using Threadia.Modules.Presence.PublicApi;

namespace Threadia.Modules.Presence;

public static class PresenceModule
{
    public static IServiceCollection AddPresenceModule(this IServiceCollection services, IConfiguration configuration)
    {
        var redisConnectionString = configuration.GetConnectionString("Redis");

        if (string.IsNullOrWhiteSpace(redisConnectionString))
        {
            services.AddSingleton<IPresenceTracker, NullPresenceTracker>();
        }
        else
        {
            // 接続は遅延確立し、Redis 停止時もアプリ起動と他機能を阻害しない。
            services.AddSingleton<IPresenceTracker>(sp =>
            {
                var lazyConnection = new Lazy<Task<IConnectionMultiplexer?>>(async () =>
                {
                    try
                    {
                        var options = ConfigurationOptions.Parse(redisConnectionString);
                        options.AbortOnConnectFail = false;
                        return await ConnectionMultiplexer.ConnectAsync(options);
                    }
                    catch (Exception ex)
                    {
                        sp.GetRequiredService<ILogger<RedisPresenceTracker>>()
                            .LogWarning(ex, "Redis への接続に失敗しました。Presence は無効になります");
                        return null;
                    }
                });

                return new RedisPresenceTracker(
                    () => lazyConnection.Value,
                    sp.GetRequiredService<ILogger<RedisPresenceTracker>>());
            });
        }

        services.AddScoped<PresenceService>();
        return services;
    }
}
