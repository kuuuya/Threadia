using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Threadia.Modules.Workspaces.Infrastructure;

/// <summary>dotnet ef コマンド用のデザインタイムファクトリ。実行時には使用しない。</summary>
public sealed class WorkspacesDbContextFactory : IDesignTimeDbContextFactory<WorkspacesDbContext>
{
    public WorkspacesDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<WorkspacesDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=threadia;Username=threadia;Password=threadia",
                o => o.MigrationsHistoryTable("__EFMigrationsHistory", WorkspacesDbContext.Schema))
            .Options;

        return new WorkspacesDbContext(options);
    }
}
