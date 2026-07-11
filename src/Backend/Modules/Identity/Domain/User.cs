namespace Threadia.Modules.Identity.Domain;

public sealed class User
{
    public const int MaxEmailLength = 254;
    public const int MaxDisplayNameLength = 50;

    public Guid Id { get; private set; }
    public string Email { get; private set; } = null!;
    public string DisplayName { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }

    private User()
    {
    }

    public static User Register(Guid id, string email, string displayName, string passwordHash, DateTime utcNow)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var trimmedName = displayName.Trim();

        if (normalizedEmail.Length is 0 or > MaxEmailLength || !normalizedEmail.Contains('@'))
        {
            throw new ArgumentException("メールアドレスの形式が不正です。", nameof(email));
        }

        if (trimmedName.Length is 0 or > MaxDisplayNameLength)
        {
            throw new ArgumentException($"表示名は1〜{MaxDisplayNameLength}文字で指定してください。", nameof(displayName));
        }

        return new User
        {
            Id = id,
            Email = normalizedEmail,
            DisplayName = trimmedName,
            PasswordHash = passwordHash,
            CreatedAt = utcNow,
        };
    }
}
