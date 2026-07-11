using Microsoft.EntityFrameworkCore;
using Threadia.Modules.Conversations.Domain;

namespace Threadia.Modules.Conversations.Infrastructure;

public sealed class ConversationsDbContext(DbContextOptions<ConversationsDbContext> options) : DbContext(options)
{
    public const string Schema = "conversations";

    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ConversationMember> ConversationMembers => Set<ConversationMember>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Conversation>(conversation =>
        {
            conversation.HasKey(c => c.Id);
            conversation.Property(c => c.Type).HasConversion<string>().HasMaxLength(20);
            conversation.Property(c => c.Name).HasMaxLength(Conversation.MaxNameLength);
            conversation.Property(c => c.DirectKey).HasMaxLength(65);
            conversation.HasIndex(c => c.WorkspaceId);

            // 同じ2ユーザー間の Direct Conversation を重複作成しない(CLAUDE.local.md)。
            conversation.HasIndex(c => new { c.WorkspaceId, c.DirectKey })
                .IsUnique()
                .HasFilter("\"DirectKey\" IS NOT NULL");
        });

        modelBuilder.Entity<ConversationMember>(member =>
        {
            member.HasKey(m => new { m.ConversationId, m.UserId });
            member.HasIndex(m => m.UserId);
            member.HasOne<Conversation>().WithMany().HasForeignKey(m => m.ConversationId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
