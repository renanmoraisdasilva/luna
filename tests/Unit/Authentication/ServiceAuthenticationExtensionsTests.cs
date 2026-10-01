using System.Security.Claims;
using FluentAssertions;
using Luna.Authentication;
using Luna.Authentication.ServiceAuthentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Validation;
using Xunit;

namespace Luna.UnitTests.Authentication;

public sealed class ServiceAuthenticationExtensionsTests
{
    [Fact]
    public void Registers_service_authentication_dependencies()
    {
        var services = new ServiceCollection();

        services.AddLunaServiceAuthentication(new ConfigurationBuilder().Build());

        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(ServiceTokenProvider));
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(IHttpClientFactory));
    }

    [Fact]
    public void Registers_jwt_validation_against_the_configured_issuer()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OpenIddict:Issuer"] = "https://identity.example/" })
            .Build();

        services.AddLunaJwtValidation(configuration);

        ResolveConfiguredIssuer(services).Should().Be("https://identity.example/");
    }

    [Fact]
    public void Registers_jwt_validation_against_the_local_development_issuer_by_default()
    {
        var services = new ServiceCollection();

        services.AddLunaJwtValidation(new ConfigurationBuilder().Build());

        ResolveConfiguredIssuer(services).Should().Be("http://localhost:5001/");
    }

    private static string ResolveConfiguredIssuer(IServiceCollection services)
    {
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<OpenIddictValidationOptions>>().Value;
        return options.Issuer!.ToString();
    }

    [Fact]
    public async Task Service_policy_accepts_matching_client_and_scope()
    {
        var policy = new AuthorizationPolicyBuilder().RequireLunaService("orders", "inventory.read").Build();
        var requirement = policy.Requirements.OfType<AssertionRequirement>().Single();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(OpenIddictConstants.Claims.ClientId, "orders"),
            new Claim(OpenIddictConstants.Claims.Scope, "catalog inventory.read"),
        ], "test"));
        var context = new AuthorizationHandlerContext(policy.Requirements, principal, null);

        (await requirement.Handler(context)).Should().BeTrue();
    }

    [Fact]
    public async Task Service_policy_rejects_wrong_client_or_scope()
    {
        var policy = new AuthorizationPolicyBuilder().RequireLunaService("orders", "inventory.read").Build();
        var requirement = policy.Requirements.OfType<AssertionRequirement>().Single();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(OpenIddictConstants.Claims.ClientId, "payments"),
            new Claim(OpenIddictConstants.Claims.Scope, "catalog"),
        ], "test"));
        var context = new AuthorizationHandlerContext(policy.Requirements, principal, null);

        (await requirement.Handler(context)).Should().BeFalse();
    }
}
