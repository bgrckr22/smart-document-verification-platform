using Microsoft.AspNetCore.Mvc;
using SmartDocumentPlatform.Api.Exceptions;

namespace SmartDocumentPlatform.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception) when (!context.Response.HasStarted && !context.RequestAborted.IsCancellationRequested)
        {
            await WriteProblemAsync(context, exception);
        }
    }

    private async Task WriteProblemAsync(HttpContext context, Exception exception)
    {
        var (statusCode, title, detail) = exception switch
        {
            AppValidationException => (StatusCodes.Status400BadRequest, "Invalid document", exception.Message),
            PayloadTooLargeException => (StatusCodes.Status413PayloadTooLarge, "Document too large", exception.Message),
            BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge } => (
                StatusCodes.Status413PayloadTooLarge,
                "Document too large",
                "The upload exceeds the configured request size limit."),
            ComputerVisionRejectedException => (
                StatusCodes.Status422UnprocessableEntity,
                "Document could not be processed",
                exception.Message),
            ResourceNotFoundException => (StatusCodes.Status404NotFound, "Resource not found", exception.Message),
            ComputerVisionException => (StatusCodes.Status502BadGateway, "Processing service unavailable", exception.Message),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected server error", "An unexpected error occurred.")
        };

        if (statusCode >= 500)
        {
            logger.LogError(exception, "Request {Method} {Path} failed", context.Request.Method, context.Request.Path);
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };
        problem.Extensions["traceId"] = context.TraceIdentifier;
        await context.Response.WriteAsJsonAsync(
            problem,
            options: null,
            contentType: "application/problem+json",
            cancellationToken: context.RequestAborted);
    }
}
