using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Threadia.Modules.Conversations.Application;
using Threadia.Modules.Conversations.Infrastructure;
using Threadia.Modules.Conversations.PublicApi;

namespace Threadia.Modules.Conversations;

public static class ConversationsModule
{
    public static IServiceCollection AddConversationsModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres が設定されていません。");

        services.AddDbContext<ConversationsDbContext>(options => options.UseNpgsql(
            connectionString,
            o => o.MigrationsHistoryTable("__EFMigrationsHistory", ConversationsDbContext.Schema)));

        services.AddScoped<ConversationService>();
        services.AddScoped<IConversationMembership, ConversationMembership>();
        services.AddScoped<IConversationDirectory, ConversationDirectory>();

        return services;
    }
}
