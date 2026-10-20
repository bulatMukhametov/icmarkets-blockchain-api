using System.Diagnostics;
using System.Threading.RateLimiting;
using Blockchain.Api.ErrorHandling;
using Blockchain.Api.Health;
using Microsoft.AspNetCore.RateLimiting;

namespace Blockchain.Api.Configuration;

public static class ApiServiceCollectionExtensions
{
    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options => options.DocumentFilter<HealthChecksDocumentFilter>());
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);
        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", tags: ["ready"],
            timeout: TimeSpan.FromSeconds(5));
        AddCors(services, configuration);
        AddCaptureRateLimit(services);
        return services;
    }

    private static void AddCors(IServiceCollection services, IConfiguration configuration)
    {
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        services.AddCors(options => options.AddDefaultPolicy(policy =>
        {
            if (origins.Length > 0) policy.WithOrigins(origins).WithMethods("GET", "POST").AllowAnyHeader();
        }));
    }

    private static void AddCaptureRateLimit(IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddConcurrencyLimiter("capture", limiter =>
            {
                limiter.PermitLimit = 4;
                limiter.QueueLimit = 0;
                limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            });
            options.OnRejected = (context, _) => new ValueTask(
                Results.Problem(statusCode: 429, title: "Too many concurrent capture requests.")
                    .ExecuteAsync(context.HttpContext));
        });
    }
}
