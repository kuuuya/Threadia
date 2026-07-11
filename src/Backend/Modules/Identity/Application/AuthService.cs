using Microsoft.EntityFrameworkCore;
using Threadia.BuildingBlocks;
using Threadia.BuildingBlocks.Exceptions;
using Threadia.Modules.Identity.Domain;
using Threadia.Modules.Identity.Infrastructure;

namespace Threadia.Modules.Identity.Application;

public sealed record AuthResult(Guid UserId, string Email, string DisplayName, string Token, DateTime ExpiresAt);

public sealed class AuthService(
    IdentityDbContext db,
    IPasswordHasher passwordHasher,
    ITokenIssuer tokenIssuer,
    TimeProvider timeProvider)
{
    public const int MinPasswordLength = 8;

    public async Task<AuthResult> RegisterAsync(string email, string displayName, string password, CancellationToken ct)
    {
        if (password.Length < MinPasswordLength)
        {
            throw new ValidationException($"パスワードは{MinPasswordLength}文字以上で指定してください。");
        }

        User user;
        try
        {
            user = User.Register(Ids.New(), email, displayName, passwordHasher.Hash(password), timeProvider.GetUtcNow().UtcDateTime);
        }
        catch (ArgumentException ex)
        {
            throw new ValidationException(ex.Message);
        }

        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException("このメールアドレスは既に登録されています。");
        }

        var (token, expiresAt) = tokenIssuer.Issue(user.Id, user.DisplayName);
        return new AuthResult(user.Id, user.Email, user.DisplayName, token, expiresAt);
    }

    public async Task<AuthResult> LoginAsync(string email, string password, CancellationToken ct)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Email == normalizedEmail, ct);

        // 存在しないユーザーとパスワード不一致を区別せず、列挙攻撃を防ぐ。
        if (user is null || !passwordHasher.Verify(password, user.PasswordHash))
        {
            throw new ValidationException("メールアドレスまたはパスワードが正しくありません。");
        }

        var (token, expiresAt) = tokenIssuer.Issue(user.Id, user.DisplayName);
        return new AuthResult(user.Id, user.Email, user.DisplayName, token, expiresAt);
    }

    public async Task<AuthResult> GetProfileAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, ct)
                   ?? throw new NotFoundException("ユーザーが見つかりません。");

        return new AuthResult(user.Id, user.Email, user.DisplayName, string.Empty, DateTime.MinValue);
    }
}
