using System.Text;
using System.Text.Json;
using FluentAssertions;
using Luna.Contracts.Authentication;
using OpenIddict.Abstractions;
using Xunit;

namespace Luna.IntegrationTests.Identity;

/// <summary>
/// Security tests for the token endpoint, exercised against the real OpenIddict pipeline.
///
/// The client-credentials grant is the authorisation boundary in front of Inventory mutations, payment
/// authorisation and shipment creation. Before these tests existed the grant trusted the caller-supplied
/// client identifier without verifying anything: <c>AcceptAnonymousClients()</c> was enabled and the
/// controller echoed <c>request.ClientId</c> into the token. Any anonymous caller could therefore mint a
/// service token for any client and any scope.
/// </summary>
[Collection(IdentityServerCollection.Name)]
public sealed class ClientCredentialsGrantTests(IdentityServerFixture fixture)
{
    private readonly IdentityServerFixture fixture = fixture;

    [Fact]
    public async Task Rejects_a_client_credentials_request_with_no_credentials()
    {
        using var response = await fixture.RequestClientCredentialsTokenAsync(
            LunaServiceClients.Orders,
            clientSecret: null,
            scope: LunaServiceScopes.PaymentsAuthorize);

        response.StatusCode.Should().BeOneOf(
            System.Net.HttpStatusCode.Unauthorized,
            System.Net.HttpStatusCode.BadRequest,
            System.Net.HttpStatusCode.Forbidden);

        (await response.Content.ReadAsStringAsync()).Should().NotContain("access_token");
    }

    [Fact]
    public async Task Rejects_an_unknown_client_identifier()
    {
        using var response = await fixture.RequestClientCredentialsTokenAsync(
            "client-that-was-never-registered",
            IdentityServerFixture.OrdersClientSecret,
            scope: LunaServiceScopes.PaymentsAuthorize);

        response.StatusCode.Should().BeOneOf(
            System.Net.HttpStatusCode.Unauthorized,
            System.Net.HttpStatusCode.BadRequest);

        (await response.Content.ReadAsStringAsync()).Should().NotContain("access_token");
    }

    [Fact]
    public async Task Rejects_a_registered_client_presenting_the_wrong_secret()
    {
        using var response = await fixture.RequestClientCredentialsTokenAsync(
            LunaServiceClients.Orders,
            "not-the-configured-secret",
            scope: LunaServiceScopes.PaymentsAuthorize);

        response.StatusCode.Should().BeOneOf(
            System.Net.HttpStatusCode.Unauthorized,
            System.Net.HttpStatusCode.BadRequest);

        (await response.Content.ReadAsStringAsync()).Should().NotContain("access_token");
    }

    [Fact]
    public async Task Issues_a_token_to_the_configured_client_presenting_the_configured_secret()
    {
        using var response = await fixture.RequestClientCredentialsTokenAsync(
            LunaServiceClients.Orders,
            IdentityServerFixture.OrdersClientSecret,
            scope: LunaServiceScopes.PaymentsAuthorize);

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var accessToken = document.RootElement.GetProperty("access_token").GetString();
        accessToken.Should().NotBeNullOrWhiteSpace();

        // The token must identify the caller as the registered service client. This is the assertion that
        // matters for the fix: before it, the token carried whatever client identifier the caller supplied,
        // which is what allowed any caller to impersonate the Orders service.
        var claims = IdentityServerFixture.ReadAccessTokenClaims(accessToken!);

        claims.Should().ContainKey(OpenIddictConstants.Claims.ClientId);
        claims[OpenIddictConstants.Claims.ClientId].GetString().Should().Be(LunaServiceClients.Orders);
    }

    [Fact]
    public async Task Issues_a_token_to_the_configured_client_when_the_secret_is_supplied_in_the_form_body()
    {
        // ServiceTokenProvider presents credentials with HTTP Basic. A form-supplied secret is the other
        // standard mechanism and must also be accepted, but only when it is correct.
        var client = fixture.CreateClient();
        using var response = await client.PostAsync(
            "/api/v1/identity/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = LunaServiceClients.Orders,
                ["client_secret"] = IdentityServerFixture.OrdersClientSecret,
                ["scope"] = LunaServiceScopes.ShippingShipmentsWrite,
            }));

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.TryGetProperty("access_token", out var accessToken).Should().BeTrue();
        accessToken.GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Rejects_a_client_presenting_the_wrong_secret_in_the_form_body()
    {
        var client = fixture.CreateClient();
        using var response = await client.PostAsync(
            "/api/v1/identity/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = LunaServiceClients.Orders,
                ["client_secret"] = "wrong-secret",
                ["scope"] = LunaServiceScopes.ShippingShipmentsWrite,
            }));

        (await response.Content.ReadAsStringAsync()).Should().NotContain("access_token");
        response.IsSuccessStatusCode.Should().BeFalse();
    }

    }