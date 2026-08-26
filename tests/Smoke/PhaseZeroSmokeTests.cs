using FluentAssertions;
using Xunit;

namespace Luna.SmokeTests;

public sealed class PhaseZeroSmokeTests
{
    [Fact]
    public void Project_should_have_a_basic_test_setup()
    {
        var value = 1 + 1;
        value.Should().Be(2);
    }

    [Fact]
    public void Smoke_test_project_should_be_configured()
    {
        var value = 2 + 2;
        value.Should().Be(4);
    }
}
