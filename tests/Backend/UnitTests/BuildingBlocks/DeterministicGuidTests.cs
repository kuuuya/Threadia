using FluentAssertions;
using Threadia.BuildingBlocks;
using Xunit;

namespace Threadia.UnitTests.BuildingBlocks;

public class DeterministicGuidTests
{
    [Fact]
    public void 同じ入力からは常に同じGuidが生成される()
    {
        DeterministicGuid.Create("event-1:user-1").Should().Be(DeterministicGuid.Create("event-1:user-1"));
    }

    [Fact]
    public void 異なる入力からは異なるGuidが生成される()
    {
        DeterministicGuid.Create("event-1:user-1").Should().NotBe(DeterministicGuid.Create("event-1:user-2"));
    }
}
