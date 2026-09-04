using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Luna.Identity.Infrastructure;

public sealed class OpenIddictSwaggerDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument document, DocumentFilterContext context)
    {
        document.Paths.Add("/.well-known/openid-configuration", new OpenApiPathItem
        {
            Operations =
            {
                [OperationType.Get] = new OpenApiOperation
                {
                    Tags = [new OpenApiTag { Name = "OpenIddict" }],
                    Summary = "Get OpenID Connect discovery metadata",
                    Responses =
                    {
                        ["200"] = new OpenApiResponse { Description = "Discovery metadata" },
                    },
                },
            },
        });

        document.Paths.Add("/.well-known/jwks", new OpenApiPathItem
        {
            Operations =
            {
                [OperationType.Get] = new OpenApiOperation
                {
                    Tags = [new OpenApiTag { Name = "OpenIddict" }],
                    Summary = "Get token signing public keys",
                    Responses =
                    {
                        ["200"] = new OpenApiResponse { Description = "JSON Web Key Set" },
                    },
                },
            },
        });
    }
}