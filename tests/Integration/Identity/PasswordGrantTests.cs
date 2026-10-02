extern alias IdentityApi;

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Luna.Contracts.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace Luna.IntegrationTests.Identity;

[Collection(IdentityServerCollection.Name)]
public sealed class PasswordGrantTests(IdentityServerFixture fixture) : IAsyncLifetime
{
    private const string Email = "password.grant.customer@example.test";
    private const string Password = "Customer-Password-123!";

    private readonly IdentityServerFixture fixture = fixture;

    public async Task InitializeAsync()
    {
        var userManager = fixture.Factory.Services
            .CreateScope()
            .ServiceProvider
            .GetRequiredService<UserManager<IdentityApi::Luna.Identity.LunaUser>>();

        if (await userManager.FindByEmailAsync(Email) is not null)
        {
            return;
        }

        var created = await userManager.CreateAsync(
            new IdentityApi::Luna.Identity.LunaUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = Email,
                Email = Email,
                EmailConfirmed = true,
                FirstName = "Password",
                LastName = "Grant",
            },
            Password);

        created.Succeeded.Should().BeTrue(
            "the probe customer could not be created: "
            + string.Join(", ", created.Errors.Select(error => error.Description)));
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Issues_an_access_token_to_a_registered_customer_using_the_storefront_client()
    {
        using var response = await fixture.RequestPasswordTokenAsync(
            Email,
            Password,
            LunaPublicClients.Storefront);

        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new InvalidOperationException(
                $"status={response.StatusCode} body={await response.Content.ReadAsStringAsync()}");
        }

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var accessToken = document.RootElement.GetProperty("access_token").GetString();
        accessToken.Should().NotBeNullOrWhiteSpace();

        var claims = IdentityServerFixture.ReadAccessTokenClaims(accessToken!);
        claims.Should().ContainKey(LunaAuthentication.SubjectClaim);
        claims[LunaAuthentication.SubjectClaim].GetString().Should().NotBeNullOrWhiteSpace();
        claims[LunaAuthentication.SubjectClaim].GetString().Should().NotBe(LunaServiceClients.Orders);
    }

    [Fact]
    public async Task Rejects_an_incorrect_password()
    {
        using var response = await fixture.RequestPasswordTokenAsync(Email, "not-the-password");

        response.IsSuccessStatusCode.Should().BeFalse();
        (await response.Content.ReadAsStringAsync()).Should().NotContain("access_token");
    }

    [Fact]
    public async Task Rejects_an_unknown_customer()
    {
        using var response = await fixture.RequestPasswordTokenAsync("nobody@example.test", Password);

        response.IsSuccessStatusCode.Should().BeFalse();
        (await response.Content.ReadAsStringAsync()).Should().NotContain("access_token");
    }

    [Fact]
    public async Task Rejects_a_password_grant_for_an_unregistered_public_client()
    {
        using var response = await fixture.RequestPasswordTokenAsync(
            Email,
            Password,
            "some-app-that-was-never-registered");

        response.IsSuccessStatusCode.Should().BeFalse();
        (await response.Content.ReadAsStringAsync()).Should().NotContain("access_token");
    }

    [Fact]
    public async Task Rejects_a_storefront_request_for_a_service_scope()
    {
        var client = fixture.CreateClient();
        using var response = await client.PostAsync(
            "/api/v1/identity/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"] = LunaPublicClients.Storefront,
                ["username"] = Email,
                ["password"] = Password,
                ["scope"] = LunaServiceScopes.PaymentsAuthorize,
            }));

        (await response.Content.ReadAsStringAsync()).Should().NotContain("access_token");
        response.IsSuccessStatusCode.Should().BeFalse(
            "the storefront client is not permitted the service-to-service payment scope");
    }

    [Fact]
    public async Task Rejects_a_storefront_client_credentials_request()
    {
        var client = fixture.CreateClient();
        using var response = await client.PostAsync(
            "/api/v1/identity/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = LunaPublicClients.Storefront,
                ["scope"] = LunaServiceScopes.PaymentsAuthorize,
            }));

        (await response.Content.ReadAsStringAsync()).Should().NotContain("access_token");
        response.IsSuccessStatusCode.Should().BeFalse(
            "a public storefront client must not be able to use the client-credentials grant");
    }
}
