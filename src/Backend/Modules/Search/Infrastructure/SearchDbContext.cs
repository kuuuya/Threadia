using Microsoft.EntityFrameworkCore;
using Threadia.Modules.Search.Domain;

namespace Threadia.Modules.Search.Infrastructure;

public sealed class SearchDbContext(DbContextOptions<SearchDbContext> options) : DbContext(options)
{
    public const string Schema = "search";

    public DbSet<MessageSearchEntry> Entries => Set<MessageSearchEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        // 日本語を含む部分一致検索のため pg_trgm を使用する(ADR 0006)。
        modelBuilder.HasPostgresExtension("pg_trgm");

        modelBuilder.Entity<MessageSearchEntry>(entry =>
        {
            entry.HasKey(e => e.MessageId);
            entry.Property(e => e.Content).IsRequired();
            entry.HasIndex(e => new { e.WorkspaceId, e.CreatedAt });
            entry.HasIndex(e => e.Content).HasMethod("gin").HasOperators("gin_trgm_ops");
        });
    }
}
