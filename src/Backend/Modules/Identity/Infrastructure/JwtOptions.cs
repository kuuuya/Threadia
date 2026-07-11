namespace Threadia.Modules.Identity.Infrastructure;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "threadia";
    public string Audience { get; set; } = "threadia";

    /// <summary>HMAC-SHA256 用の署名鍵。本番では環境変数またはシークレットストアから注入する。</summary>
    public string SigningKey { get; set; } = string.Empty;

    public int ExpiryMinutes { get; set; } = 60 * 12;
}
