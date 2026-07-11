using FluentAssertions;
using Threadia.Modules.Identity.Domain;
using Threadia.Modules.Identity.Infrastructure;
using Xunit;

namespace Threadia.UnitTests.Identity;

public class UserTests
{
    private static readonly DateTime Now = new(2026, 7, 11, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Register_メールアドレスは小文字へ正規化される()
    {
        var user = User.Register(Guid.NewGuid(), " Alice@Example.COM ", "Alice", "hash", Now);

        user.Email.Should().Be("alice@example.com");
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void Register_不正なメールアドレス_例外を送出する(string email)
    {
        var act = () => User.Register(Guid.NewGuid(), email, "Alice", "hash", Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Register_空の表示名_例外を送出する()
    {
        var act = () => User.Register(Guid.NewGuid(), "alice@example.com", "  ", "hash", Now);

        act.Should().Throw<ArgumentException>();
    }
}

public class Pbkdf2PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void Verify_正しいパスワード_trueを返す()
    {
        var hash = _hasher.Hash("correct-password");

        _hasher.Verify("correct-password", hash).Should().BeTrue();
    }

    [Fact]
    public void Verify_誤ったパスワード_falseを返す()
    {
        var hash = _hasher.Hash("correct-password");

        _hasher.Verify("wrong-password", hash).Should().BeFalse();
    }

    [Fact]
    public void Verify_不正な形式のハッシュ_falseを返す()
    {
        _hasher.Verify("password", "broken-hash").Should().BeFalse();
    }

    [Fact]
    public void Hash_同じパスワードでも異なるソルトで異なるハッシュになる()
    {
        _hasher.Hash("password").Should().NotBe(_hasher.Hash("password"));
    }
}
