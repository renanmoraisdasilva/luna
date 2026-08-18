using FluentAssertions;
using Xunit;

namespace Luna.UnitTests;

public class PhaseZeroSmokeTests
{
    [Fact]
    public void Project_should_have_a_basic_test_setup()
    {
        var value = 1 + 1;
        value.Should().Be(2);
    }
}
