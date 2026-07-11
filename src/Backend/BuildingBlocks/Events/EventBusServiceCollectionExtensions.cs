using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Threadia.BuildingBlocks.Events;

public static class EventBusServiceCollectionExtensions
{
    /// <summary>
    /// EventBus:Provider の設定でバス実装を切り替える。
    /// - "RabbitMq": プロセス間配信。Consumer は RabbitMqConsumerHost を持つホスト(Workers)で動く。
    /// - "InProcess"(既定): 同一プロセス内で Consumer を直接呼び出す。開発・テスト用。
    /// </summary>
    public static IServiceCollection AddIntegrationEventBus(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["EventBus:Provider"] ?? "InProcess";

        if (string.Equals(provider, "RabbitMq", StringComparison.OrdinalIgnoreCase))
        {
            services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));
            services.AddSingleton<RabbitMqConnectionProvider>();
            services.AddSingleton<IIntegrationEventBus, RabbitMqEventBus>();
        }
        else
        {
            services.AddSingleton<IIntegrationEventBus, InProcessEventBus>();
        }

        return services;
    }

    /// <summary>RabbitMQ からの消費ホストを登録する(Workers 用)。InProcess 構成では登録不要。</summary>
    public static IServiceCollection AddRabbitMqConsumerHost(this IServiceCollection services)
    {
        services.AddHostedService<RabbitMqConsumerHost>();
        return services;
    }

    public static IServiceCollection AddIntegrationEventConsumer<TConsumer>(this IServiceCollection services)
        where TConsumer : class, IIntegrationEventConsumer
    {
        services.AddScoped<IIntegrationEventConsumer, TConsumer>();
        return services;
    }
}
