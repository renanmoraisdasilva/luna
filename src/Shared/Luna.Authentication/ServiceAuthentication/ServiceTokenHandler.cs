using System.Net.Http.Headers;

namespace Luna.Authentication.ServiceAuthentication;

public sealed class ServiceTokenHandler(
    ServiceTokenProvider tokenProvider,
    string scope) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = (await tokenProvider.GetTokenAsync(scope, cancellationToken)).Trim();
        request.Headers.Remove("Authorization");
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {token}");
        return await base.SendAsync(request, cancellationToken);
    }
}
