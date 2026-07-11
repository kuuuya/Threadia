using Microsoft.EntityFrameworkCore;
using Threadia.Modules.Identity.PublicApi;

namespace Threadia.Modules.Identity.Infrastructure;

public sealed class UserDirectory(IdentityDbContext db) : IUserDirectory
{
    public async Task<UserSummary?> FindByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        return await db.Users.AsNoTracking()
            .Where(u => u.Email == normalizedEmail)
            .Select(u => new UserSummary(u.Id, u.Email, u.DisplayName))
            .SingleOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<UserSummary>> GetByIdsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
    {
        return await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new UserSummary(u.Id, u.Email, u.DisplayName))
            .ToListAsync(ct);
    }

    public Task<bool> ExistsAsync(Guid userId, CancellationToken ct = default) =>
        db.Users.AsNoTracking().AnyAsync(u => u.Id == userId, ct);
}
