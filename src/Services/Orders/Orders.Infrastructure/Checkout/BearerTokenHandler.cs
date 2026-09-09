using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;

namespace Luna.Orders.Infrastructure.Checkout;

public sealed class BearerTokenHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var authorization = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (AuthenticationHeaderValue.TryParse(authorization, out var bearer))
        {
            request.Headers.Authorization = bearer;
        }

        return base.SendAsync(request, cancellationToken);
    }
}
