namespace Threadia.Modules.Identity.Application;

public interface ITokenIssuer
{
    /// <returns>アクセストークンと有効期限(UTC)。</returns>
    (string Token, DateTime ExpiresAt) Issue(Guid userId, string displayName);
}
