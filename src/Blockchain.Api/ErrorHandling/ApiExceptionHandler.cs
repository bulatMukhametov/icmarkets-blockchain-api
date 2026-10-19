using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Blockchain.Application.Providers;
using Blockchain.Application.Validation;
using FluentValidation;
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

        var detail = CreateProblem(context, exception);
        context.Response.StatusCode = detail.Status!.Value;
        detail.Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;
        if (await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context, ProblemDetails = detail, Exception = exception
        })) return true;

        // Keep the API error contract even when no registered writer accepts the request's Accept header.
        await context.Response.WriteAsJsonAsync((object)detail, options: null,
            contentType: "application/problem+json", cancellationToken: token);
        return true;
    }

    private ProblemDetails CreateProblem(HttpContext context, Exception exception) => exception switch
    {
        ValidationException validation => CreateValidationProblem(validation),
        RequestValidationException validation => CreateValidationProblem(
            new Dictionary<string, string[]> { [validation.Field] = [validation.Message] }),
        ProviderCircuitOpenException circuit => CreateCircuitOpenProblem(context, circuit),
        ProviderException provider => CreateProviderProblem(context, provider),
        _ => CreateUnexpectedProblem(context, exception)
    };

    private static ValidationProblemDetails CreateValidationProblem(ValidationException exception)
    {
        var errors = exception.Errors
            .GroupBy(error => GetParameterName(error.PropertyName))
            .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).Distinct().ToArray());
        return CreateValidationProblem(errors);
    }

    private static ValidationProblemDetails CreateValidationProblem(IDictionary<string, string[]> errors) =>
        new(errors) { Status = 400, Title = "Request validation failed." };

    private static string GetParameterName(string propertyName) => propertyName switch
    {
        "CurrencyCode" => "coin",
        "NetworkCode" => "network",
        _ => JsonNamingPolicy.CamelCase.ConvertName(propertyName)
    };

    private static ProblemDetails CreateCircuitOpenProblem(HttpContext context, ProviderCircuitOpenException exception)
    {
        context.Response.Headers.RetryAfter = Math.Ceiling(exception.RetryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        return new ProblemDetails { Status = 503, Title = "Provider temporarily unavailable; retry later." };
    }

    private ProblemDetails CreateProviderProblem(HttpContext context, ProviderException exception)
    {
        logger.LogWarning(exception, "Provider operation failed for {TraceId}", context.TraceIdentifier);
        return new ProblemDetails
        {
            Status = exception.TimedOut ? 504 : 502,
            Title = exception.TimedOut ? "Provider request timed out." : "Provider data is unavailable."
        };
    }

    private ProblemDetails CreateUnexpectedProblem(HttpContext context, Exception exception)
    {
        logger.LogError(exception, "Request failed for {TraceId}", context.TraceIdentifier);
        return new ProblemDetails { Status = 500, Title = "An unexpected error occurred." };
    }
}
