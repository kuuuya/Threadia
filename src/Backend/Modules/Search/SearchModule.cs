using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Threadia.BuildingBlocks.Events;
using Threadia.Modules.Search.Application;
using Threadia.Modules.Search.Infrastructure;

namespace Threadia.Modules.Search;

public static class SearchModule
{
    public static IServiceCollection AddSearchModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres が設定されていません。");

        services.AddDbContext<SearchDbContext>(options => options.UseNpgsql(
            connectionString,
            o => o.MigrationsHistoryTable("__EFMigrationsHistory", SearchDbContext.Schema)));

        services.AddScoped<SearchService>();
        services.AddIntegrationEventConsumer<SearchIndexConsumer>();

        return services;
    }
}
