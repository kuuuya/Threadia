using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Threadia.Modules.Attachments.Infrastructure;

/// <summary>dotnet ef コマンド用のデザインタイムファクトリ。実行時には使用しない。</summary>
public sealed class AttachmentsDbContextFactory : IDesignTimeDbContextFactory<AttachmentsDbContext>
{
    public AttachmentsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AttachmentsDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=threadia;Username=threadia;Password=threadia",
                o => o.MigrationsHistoryTable("__EFMigrationsHistory", AttachmentsDbContext.Schema))
            .Options;

        return new AttachmentsDbContext(options);
    }
}
