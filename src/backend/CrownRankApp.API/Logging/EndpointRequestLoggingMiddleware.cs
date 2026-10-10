using Microsoft.AspNetCore.Mvc.Controllers;

namespace CrownRankApp.API.Logging;

public sealed class EndpointRequestLoggingMiddleware(RequestDelegate next, TimeProvider clock)
{
    public async Task InvokeAsync(HttpContext context, DailyEndpointLogWriter logs)
    {
        try
        {
            await next(context);
        }
        finally
        {
            var action = context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();
            var source = string.Equals(context.Request.Headers["X-CrownRank-Client"], "frontend", StringComparison.OrdinalIgnoreCase)
                ? "frontend"
                : "backend";

            var entry = new EndpointLogEntry(
                clock.GetLocalNow(),
                EndpointAuditDescription.GetText(action, context.Response.StatusCode),
                EndpointAuditDescription.GetWho(context, action),
                EndpointAuditDescription.GetEndpoint(context, action),
                context.Response.StatusCode < 400 ? "Success" : "Fail",
                EndpointAuditDescription.GetIds(context));

            await logs.WriteAsync("Backend", entry, context.RequestAborted);

            if (source == "frontend")
            {
                await logs.WriteAsync("Frontend", entry, context.RequestAborted);
            }
        }
    }
}
