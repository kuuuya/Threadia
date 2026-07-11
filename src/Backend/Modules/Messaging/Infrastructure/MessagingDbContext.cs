using Microsoft.EntityFrameworkCore;
using Threadia.Modules.Messaging.Domain;

namespace Threadia.Modules.Messaging.Infrastructure;

public sealed class MessagingDbContext(DbContextOptions<MessagingDbContext> options) : DbContext(options)
{
    public const string Schema = "messaging";

    public DbSet<Message> Messages => Set<Message>();
    public DbSet<MessageMention> MessageMentions => Set<MessageMention>();
    public DbSet<ReadPosition> ReadPositions => Set<ReadPosition>();
    public DbSet<ConversationSequence> ConversationSequences => Set<ConversationSequence>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Message>(message =>
        {
            message.HasKey(m => m.Id);
            message.Property(m => m.Content).HasMaxLength(Message.MaxContentLength).IsRequired();
            message.Property(m => m.ClientMessageId).HasMaxLength(Message.MaxClientMessageIdLength).IsRequired();

            // Conversation 内の順序保証。履歴取得の基本インデックスを兼ねる。
            message.HasIndex(m => new { m.ConversationId, m.Sequence }).IsUnique();

            // ClientMessageId 再送時の重複登録防止。
            message.HasIndex(m => new { m.SenderId, m.ClientMessageId }).IsUnique();
        });

        modelBuilder.Entity<MessageMention>(mention =>
        {
            mention.HasKey(m => new { m.MessageId, m.MentionedUserId });
            mention.HasIndex(m => m.MentionedUserId);
            mention.HasOne<Message>().WithMany().HasForeignKey(m => m.MessageId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReadPosition>(position =>
        {
            position.HasKey(p => new { p.UserId, p.ConversationId });
        });

        modelBuilder.Entity<ConversationSequence>(sequence =>
        {
            sequence.HasKey(s => s.ConversationId);
        });

        modelBuilder.Entity<OutboxMessage>(outbox =>
        {
            outbox.HasKey(o => o.Id);
            outbox.Property(o => o.Type).HasMaxLength(100).IsRequired();
            outbox.Property(o => o.Payload).HasColumnType("jsonb").IsRequired();
            outbox.Property(o => o.LastError).HasMaxLength(2000);

            // 未処理行のポーリング用。処理済み行はインデックスに含めない。
            outbox.HasIndex(o => o.CreatedAt)
                .HasFilter("\"ProcessedAt\" IS NULL AND \"DeadLetteredAt\" IS NULL");
        });
    }
}
