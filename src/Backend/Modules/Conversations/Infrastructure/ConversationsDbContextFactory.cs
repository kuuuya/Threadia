using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Threadia.Modules.Conversations.Infrastructure;

/// <summary>dotnet ef コマンド用のデザインタイムファクトリ。実行時には使用しない。</summary>
public sealed class ConversationsDbContextFactory : IDesignTimeDbContextFactory<ConversationsDbContext>
{
    public ConversationsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ConversationsDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=threadia;Username=threadia;Password=threadia",
                o => o.MigrationsHistoryTable("__EFMigrationsHistory", ConversationsDbContext.Schema))
            .Options;

        return new ConversationsDbContext(options);
    }
}
