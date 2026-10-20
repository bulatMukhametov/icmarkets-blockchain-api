using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Blockchain.Api.Health;

// Health-check middleware endpoints are not discovered by MVC's API explorer.
public sealed class HealthChecksDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument document, DocumentFilterContext context)
    {
        document.Paths["/health/live"] = CreatePath(
            "Check whether the application is running.", includeUnavailable: false);
        document.Paths["/health/ready"] = CreatePath(
            "Check whether the database and required schema are available.", includeUnavailable: true);
    }

    private static OpenApiPathItem CreatePath(string summary, bool includeUnavailable)
    {
        var responses = new OpenApiResponses
        {
            ["200"] = CreateResponse("Healthy")
        };
        if (includeUnavailable)
        {
            responses["503"] = CreateResponse("Unhealthy");
        }

        return new OpenApiPathItem
        {
            Operations = new()
            {
                [HttpMethod.Get] = new OpenApiOperation { Summary = summary, Responses = responses }
            }
        };
    }

    private static OpenApiResponse CreateResponse(string description) => new()
    {
        Description = description,
        Content = new Dictionary<string, OpenApiMediaType>
        {
            ["text/plain"] = new()
            {
                Schema = new OpenApiSchema { Type = JsonSchemaType.String }
            }
        }
    };
}
