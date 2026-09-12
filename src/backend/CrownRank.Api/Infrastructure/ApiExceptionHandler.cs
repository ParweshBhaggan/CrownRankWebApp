using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CrownRank.Api.Infrastructure;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception,
        CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            ApiProblemException known => new ProblemDetails
            {
                Status = known.StatusCode, Title = known.Title, Detail = known.Message
            },
            OperationCanceledException when context.RequestAborted.IsCancellationRequested => null,
            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Unexpected server error",
                Detail = "The request could not be completed. Please retry."
            }
        };

        if (problem is null) return false;
        if (problem.Status >= 500) logger.LogError(exception, "Unhandled API exception");
        else logger.LogInformation("API request rejected: {Detail}", problem.Detail);

        context.Response.StatusCode = problem.Status!.Value;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
