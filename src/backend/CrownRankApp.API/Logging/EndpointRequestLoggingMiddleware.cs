using System.Diagnostics;

namespace CrownRankApp.API.Logging;

public sealed class EndpointRequestLoggingMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, DailyEndpointLogWriter logs)
    {
        var started = Stopwatch.GetTimestamp();

        try
        {
            await next(context);
        }
        finally
        {
            var elapsed = Stopwatch.GetElapsedTime(started);
            var endpointName = context.GetEndpoint()?.DisplayName;
            var source = string.Equals(context.Request.Headers["X-CrownRank-Client"], "frontend", StringComparison.OrdinalIgnoreCase)
                ? "frontend"
                : "backend";

            var entry = new EndpointLogEntry(
                DateTimeOffset.Now,
                context.Request.Method,
                context.Request.Path.Value ?? "/",
                context.Response.StatusCode,
                (long)Math.Round(elapsed.TotalMilliseconds),
                context.TraceIdentifier,
                source,
                endpointName);

            await logs.WriteAsync("Backend", entry, context.RequestAborted);

            if (source == "frontend")
            {
                await logs.WriteAsync("Frontend", entry, context.RequestAborted);
            }
        }
    }
}
