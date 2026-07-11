using FluentAssertions;
using Threadia.Modules.Messaging.Domain;
using Xunit;

namespace Threadia.UnitTests.Messaging;

public class OutboxMessageTests
{
    private static readonly DateTime Now = new(2026, 7, 11, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void MarkFailed_リトライ回数が上限未満_バックオフ付きで再試行予定になる()
    {
        var outbox = OutboxMessage.Create(Guid.NewGuid(), "message.sent", "{}", Now);

        outbox.MarkFailed("error", Now);

        outbox.Attempts.Should().Be(1);
        outbox.DeadLetteredAt.Should().BeNull();
        outbox.NextAttemptAt.Should().Be(Now.AddSeconds(2));
    }

    [Fact]
    public void MarkFailed_リトライ上限到達_DeadLetter状態になる()
    {
        var outbox = OutboxMessage.Create(Guid.NewGuid(), "message.sent", "{}", Now);

        for (var i = 0; i < OutboxMessage.MaxAttempts; i++)
        {
            outbox.MarkFailed("error", Now.AddSeconds(i));
        }

        outbox.Attempts.Should().Be(OutboxMessage.MaxAttempts);
        outbox.DeadLetteredAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkProcessed_処理済み時刻が記録される()
    {
        var outbox = OutboxMessage.Create(Guid.NewGuid(), "message.sent", "{}", Now);

        outbox.MarkProcessed(Now.AddSeconds(1));

        outbox.ProcessedAt.Should().Be(Now.AddSeconds(1));
    }
}
