using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Luna.Identity.Infrastructure;

public static class IdentityEndpointExtensions
{
    public static IEndpointConventionBuilder ExcludeIdentityRegistration(
        this IEndpointConventionBuilder endpoints)
    {
        endpoints.Add(endpointBuilder =>
        {
            if (endpointBuilder is not RouteEndpointBuilder routeEndpointBuilder ||
                routeEndpointBuilder.RoutePattern.RawText != "/api/v1/identity/register")
            {
                return;
            }

            endpointBuilder.Metadata.Add(new SuppressMatchingMetadata());
            endpointBuilder.Metadata.Add(new ApiExplorerSettingsAttribute { IgnoreApi = true });
        });

        return endpoints;
    }
}