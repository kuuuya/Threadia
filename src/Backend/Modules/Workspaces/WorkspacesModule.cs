using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Threadia.Modules.Workspaces.Application;
using Threadia.Modules.Workspaces.Infrastructure;
using Threadia.Modules.Workspaces.PublicApi;

namespace Threadia.Modules.Workspaces;

public static class WorkspacesModule
{
    public static IServiceCollection AddWorkspacesModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres が設定されていません。");

        services.AddDbContext<WorkspacesDbContext>(options => options.UseNpgsql(
            connectionString,
            o => o.MigrationsHistoryTable("__EFMigrationsHistory", WorkspacesDbContext.Schema)));

        services.AddScoped<WorkspaceService>();
        services.AddScoped<IWorkspaceMembership, WorkspaceMembership>();

        return services;
    }
}
