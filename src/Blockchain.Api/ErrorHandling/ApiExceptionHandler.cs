using Blockchain.Application.Abstractions;
using System.Globalization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Blockchain.Api.ErrorHandling;

public sealed class ApiExceptionHandler(IProblemDetailsService problems, ILogger<ApiExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken token)
    {
        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
        {
            context.Response.StatusCode = 499;
            return true;
        }
        ProblemDetails detail;
        if (exception is RequestValidationException validation)
        {
            detail = new ValidationProblemDetails(new Dictionary<string, string[]> { [validation.Field] = [validation.Message] })
            { Status = 400, Title = "Request validation failed." };
        }
        else if (exception is ProviderCircuitOpenException circuit)
        {
            detail = new ProblemDetails { Status = 503, Title = "Provider temporarily unavailable; retry later." };
            context.Response.Headers.RetryAfter = Math.Ceiling(circuit.RetryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }
        else if (exception is ProviderException provider)
        {
            detail = new ProblemDetails
            {
                Status = provider.TimedOut ? 504 : 502,
                Title = provider.TimedOut ? "Provider request timed out." : "Provider data is unavailable."
            };
            logger.LogWarning(exception, "Provider operation failed for {TraceId}", context.TraceIdentifier);
        }
        else
        {
            detail = new ProblemDetails { Status = 500, Title = "An unexpected error occurred." };
            logger.LogError(exception, "Request failed for {TraceId}", context.TraceIdentifier);
        }
        context.Response.StatusCode = detail.Status!.Value;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context, ProblemDetails = detail, Exception = exception
        });
    }
}
