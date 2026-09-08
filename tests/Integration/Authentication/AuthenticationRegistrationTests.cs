using FluentAssertions;
using Luna.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Luna.IntegrationTests.Authentication;

public sealed class AuthenticationRegistrationTests
{
    [Fact]
    public void Uses_the_default_issuer_when_configuration_is_missing()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        var result = services.AddLunaJwtValidation(configuration);

        result.Should().BeSameAs(services);
    }
}
