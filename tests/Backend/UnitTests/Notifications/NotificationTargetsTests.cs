using FluentAssertions;
using Threadia.Modules.Notifications.Application;
using Threadia.Modules.Notifications.Domain;
using Xunit;

namespace Threadia.UnitTests.Notifications;

public class NotificationTargetsTests
{
    private static readonly Guid Sender = Guid.NewGuid();
    private static readonly Guid MemberA = Guid.NewGuid();
    private static readonly Guid MemberB = Guid.NewGuid();

    [Fact]
    public void DirectConversation_相手にdirect_message通知が作られ送信者は対象外()
    {
        var targets = NotificationTargets.Resolve("Direct", [Sender, MemberA], Sender, []);

        targets.Should().ContainSingle();
        targets[0].UserId.Should().Be(MemberA);
        targets[0].Type.Should().Be(NotificationTypes.DirectMessage);
    }

    [Fact]
    public void GroupConversation_メンションなしなら通知対象はいない()
    {
        var targets = NotificationTargets.Resolve("Group", [Sender, MemberA, MemberB], Sender, []);

        targets.Should().BeEmpty();
    }

    [Fact]
    public void メンションされた参加者にmention通知が作られる()
    {
        var targets = NotificationTargets.Resolve("Group", [Sender, MemberA, MemberB], Sender, [MemberA]);

        targets.Should().ContainSingle();
        targets[0].UserId.Should().Be(MemberA);
        targets[0].Type.Should().Be(NotificationTypes.Mention);
    }

    [Fact]
    public void Directでメンションされた場合はmentionが優先される()
    {
        var targets = NotificationTargets.Resolve("Direct", [Sender, MemberA], Sender, [MemberA]);

        targets.Should().ContainSingle();
        targets[0].Type.Should().Be(NotificationTypes.Mention);
    }

    [Fact]
    public void 非参加者へのメンションは通知対象にならない()
    {
        var outsider = Guid.NewGuid();

        var targets = NotificationTargets.Resolve("Group", [Sender, MemberA], Sender, [outsider]);

        targets.Should().BeEmpty();
    }

    [Fact]
    public void 自分自身へのメンションは通知対象にならない()
    {
        var targets = NotificationTargets.Resolve("Group", [Sender, MemberA], Sender, [Sender]);

        targets.Should().BeEmpty();
    }
}
