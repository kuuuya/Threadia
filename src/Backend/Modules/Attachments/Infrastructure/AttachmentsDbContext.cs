using Microsoft.EntityFrameworkCore;
using Threadia.Modules.Attachments.Domain;

namespace Threadia.Modules.Attachments.Infrastructure;

public sealed class AttachmentsDbContext(DbContextOptions<AttachmentsDbContext> options) : DbContext(options)
{
    public const string Schema = "attachments";

    public DbSet<Attachment> Attachments => Set<Attachment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Attachment>(attachment =>
        {
            attachment.HasKey(a => a.Id);
            attachment.Property(a => a.FileName).HasMaxLength(AttachmentPolicy.MaxFileNameLength).IsRequired();
            attachment.Property(a => a.ContentType).HasMaxLength(150).IsRequired();
            attachment.Property(a => a.StorageKey).HasMaxLength(300).IsRequired();
            attachment.HasIndex(a => a.MessageId);

            // 孤立ファイル掃除(MessageId IS NULL かつ古い行)用。
            attachment.HasIndex(a => a.CreatedAt).HasFilter("\"MessageId\" IS NULL");
        });
    }
}
