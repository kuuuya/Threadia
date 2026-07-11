using FluentAssertions;
using Threadia.Modules.Conversations.Domain;
using Xunit;

namespace Threadia.UnitTests.Conversations;

public class ConversationTests
{
    private static readonly DateTime Now = new(2026, 7, 11, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void BuildDirectKey_ユーザーIDの順序に依存せず同じキーを返す()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        Conversation.BuildDirectKey(userA, userB).Should().Be(Conversation.BuildDirectKey(userB, userA));
    }

    [Fact]
    public void CreateDirect_自分自身との会話_例外を送出する()
    {
        var userId = Guid.NewGuid();

        var act = () => Conversation.CreateDirect(Guid.NewGuid(), Guid.NewGuid(), userId, userId, Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateDirect_DirectKeyが設定されNameはnull()
    {
        var conversation = Conversation.CreateDirect(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now);

        conversation.Type.Should().Be(ConversationType.Direct);
        conversation.DirectKey.Should().NotBeNull();
        conversation.Name.Should().BeNull();
    }

    [Fact]
    public void CreateGroup_名前が空_例外を送出する()
    {
        var act = () => Conversation.CreateGroup(Guid.NewGuid(), Guid.NewGuid(), "  ", Guid.NewGuid(), Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Leave後にRejoin_参加中に戻りJoinedAtは初回参加時刻を保持する()
    {
        var member = ConversationMember.Create(Guid.NewGuid(), Guid.NewGuid(), Now);

        member.Leave(Now.AddDays(1));
        member.IsActive.Should().BeFalse();

        member.Rejoin();

        member.IsActive.Should().BeTrue();
        member.JoinedAt.Should().Be(Now);
    }
}
