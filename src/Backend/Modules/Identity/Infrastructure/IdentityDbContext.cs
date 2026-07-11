using Microsoft.EntityFrameworkCore;
using Threadia.Modules.Identity.Domain;

namespace Threadia.Modules.Identity.Infrastructure;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public const string Schema = "identity";

    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<User>(user =>
        {
            user.HasKey(u => u.Id);
            user.Property(u => u.Email).HasMaxLength(User.MaxEmailLength).IsRequired();
            user.Property(u => u.DisplayName).HasMaxLength(User.MaxDisplayNameLength).IsRequired();
            user.Property(u => u.PasswordHash).HasMaxLength(500).IsRequired();
            user.HasIndex(u => u.Email).IsUnique();
        });
    }
}
