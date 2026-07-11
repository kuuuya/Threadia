using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Threadia.Modules.Messaging.Infrastructure;

/// <summary>dotnet ef コマンド用のデザインタイムファクトリ。実行時には使用しない。</summary>
public sealed class MessagingDbContextFactory : IDesignTimeDbContextFactory<MessagingDbContext>
{
    public MessagingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<MessagingDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=threadia;Username=threadia;Password=threadia",
                o => o.MigrationsHistoryTable("__EFMigrationsHistory", MessagingDbContext.Schema))
            .Options;

        return new MessagingDbContext(options);
    }
}
