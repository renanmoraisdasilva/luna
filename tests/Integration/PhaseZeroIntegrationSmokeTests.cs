using FluentAssertions;
using Xunit;

namespace Luna.IntegrationTests;

public class PhaseZeroIntegrationSmokeTests
{
    [Fact]
    public void Integration_test_project_should_be_configured()
    {
        var value = 2 + 2;
        value.Should().Be(4);
    }
}
