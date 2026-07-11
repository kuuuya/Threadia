using FluentAssertions;
using Threadia.Modules.Messaging.Domain;
using Xunit;

namespace Threadia.UnitTests.Messaging;

public class MessageTests
{
    private static readonly DateTime Now = new(2026, 7, 11, 0, 0, 0, DateTimeKind.Utc);

    private static Message CreateMessage(string content = "hello") =>
        Message.Create(Guid.NewGuid(), Guid.NewGuid(), 1, Guid.NewGuid(), "client-1", content, Now);

    [Fact]
    public void Create_空白のみの本文_例外を送出する()
    {
        var act = () => CreateMessage("   ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_上限超過の本文_例外を送出する()
    {
        var act = () => CreateMessage(new string('a', Message.MaxContentLength + 1));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Edit_本文が更新されEditedAtが記録される_Sequenceは変わらない()
    {
        var message = CreateMessage();
        var originalSequence = message.Sequence;

        message.Edit("updated", Now.AddMinutes(1));

        message.Content.Should().Be("updated");
        message.EditedAt.Should().Be(Now.AddMinutes(1));
        message.Sequence.Should().Be(originalSequence);
    }

    [Fact]
    public void Edit_削除済みメッセージ_例外を送出する()
    {
        var message = CreateMessage();
        message.Delete(Now.AddMinutes(1));

        var act = () => message.Edit("updated", Now.AddMinutes(2));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Delete_2回呼んでも最初の削除時刻を保持する()
    {
        var message = CreateMessage();

        message.Delete(Now.AddMinutes(1));
        message.Delete(Now.AddMinutes(2));

        message.DeletedAt.Should().Be(Now.AddMinutes(1));
        message.IsDeleted.Should().BeTrue();
    }
}
