using System.Net.Http;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Luna.Identity.Infrastructure;

public sealed class OpenIddictSwaggerDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument document, DocumentFilterContext context)
    {
        document.Tags ??= new HashSet<OpenApiTag>();
        document.Tags.Add(new OpenApiTag { Name = "OpenIddict" });

        AddGetOperation(
            document,
            "/.well-known/openid-configuration",
            "Get OpenID Connect discovery metadata",
            "Discovery metadata");

        AddGetOperation(
            document,
            "/.well-known/jwks",
            "Get token signing public keys",
            "JSON Web Key Set");
    }

    private static void AddGetOperation(
        OpenApiDocument document,
        string path,
        string summary,
        string responseDescription)
    {
        var operation = new OpenApiOperation
        {
            Tags = new HashSet<OpenApiTagReference>
            {
                new("OpenIddict", document),
            },
            Summary = summary,
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse { Description = responseDescription },
            },
        };

        document.Paths.Add(path, new OpenApiPathItem
        {
            Operations = new Dictionary<HttpMethod, OpenApiOperation>
            {
                [HttpMethod.Get] = operation,
            },
        });
    }
}
