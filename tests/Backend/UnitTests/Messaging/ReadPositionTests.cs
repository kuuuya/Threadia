using FluentAssertions;
using Threadia.Modules.Messaging.Domain;
using Xunit;

namespace Threadia.UnitTests.Messaging;

public class ReadPositionTests
{
    private static readonly DateTime Now = new(2026, 7, 11, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Advance_前方への更新_LastReadSequenceが進む()
    {
        var position = ReadPosition.Create(Guid.NewGuid(), Guid.NewGuid(), 5, Now);

        position.Advance(10, Now.AddMinutes(1));

        position.LastReadSequence.Should().Be(10);
        position.UpdatedAt.Should().Be(Now.AddMinutes(1));
    }

    [Fact]
    public void Advance_後方への更新要求_LastReadSequenceは後退しない()
    {
        var position = ReadPosition.Create(Guid.NewGuid(), Guid.NewGuid(), 10, Now);

        position.Advance(3, Now.AddMinutes(1));

        position.LastReadSequence.Should().Be(10);
        position.UpdatedAt.Should().Be(Now);
    }

    [Fact]
    public void Advance_同値への更新要求_変化しない()
    {
        var position = ReadPosition.Create(Guid.NewGuid(), Guid.NewGuid(), 10, Now);

        position.Advance(10, Now.AddMinutes(1));

        position.LastReadSequence.Should().Be(10);
        position.UpdatedAt.Should().Be(Now);
    }

    [Fact]
    public void Create_負の既読位置_例外を送出する()
    {
        var act = () => ReadPosition.Create(Guid.NewGuid(), Guid.NewGuid(), -1, Now);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
