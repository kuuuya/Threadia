using Microsoft.EntityFrameworkCore;
using Threadia.Modules.Workspaces.Domain;

namespace Threadia.Modules.Workspaces.Infrastructure;

public sealed class WorkspacesDbContext(DbContextOptions<WorkspacesDbContext> options) : DbContext(options)
{
    public const string Schema = "workspaces";

    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<WorkspaceMember> WorkspaceMembers => Set<WorkspaceMember>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Workspace>(workspace =>
        {
            workspace.HasKey(w => w.Id);
            workspace.Property(w => w.Name).HasMaxLength(Workspace.MaxNameLength).IsRequired();
        });

        modelBuilder.Entity<WorkspaceMember>(member =>
        {
            member.HasKey(m => new { m.WorkspaceId, m.UserId });
            member.HasIndex(m => m.UserId);
            member.Property(m => m.Role).HasConversion<string>().HasMaxLength(20);
            member.HasOne<Workspace>().WithMany().HasForeignKey(m => m.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
