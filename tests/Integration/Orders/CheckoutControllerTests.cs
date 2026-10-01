using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Luna.Orders.Contracts.Checkout;
using Xunit;

namespace Luna.IntegrationTests.Orders;

[Collection(OrdersDatabaseCollection.Name)]
public sealed class CheckoutControllerTests(OrdersSqlServerFixture fixture) : IAsyncLifetime
{
    /// <summary>
    /// The database is reset as a lifecycle hook rather than as the first statement of each test, so a
    /// test that throws during setup cannot leak rows into the next one.
    /// </summary>
    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;
    [Fact]
    public async Task Checkout_without_a_customer_subject_is_rejected_with_a_conflict()
    {
        using var factory = new OrdersApiFactory(fixture);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.AdminHeader, "true");

        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders/checkout")
        {
            Content = JsonContent.Create(ValidRequest()),
        };
        message.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var response = await client.SendAsync(message);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var error = await response.Content.ReadFromJsonAsync<ErrorDto>();
        error!.Code.Should().Be("FULFILLMENT_CONFLICT");
    }

    [Fact]
    public async Task Checkout_without_an_idempotency_key_is_rejected_as_unprocessable()
    {
        using var factory = new OrdersApiFactory(fixture);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.CustomerHeader, Guid.NewGuid().ToString());

        var response = await client.PostAsJsonAsync("/api/v1/orders/checkout", ValidRequest());

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var error = await response.Content.ReadFromJsonAsync<ErrorDto>();
        error!.Code.Should().Be("INVALID_IDEMPOTENCY_KEY");
        error.Message.Should().NotBeNullOrEmpty();
    }

    private static CheckoutRequest ValidRequest() => new(
        "Jane Doe",
        "123 Luna Street",
        null,
        "Austin",
        "Texas",
        "78701",
        "US",
        "STANDARD",
        "test-card",
        "USD");

    private sealed record ErrorDto(string Code, string Message);
}
