using System.Security.Cryptography;
using System.Text;
using CrownRankApp.API.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CrownRankApp.API;

public sealed class AdminApiKeyFilter(IConfiguration configuration) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        var action = context.ActionDescriptor.RouteValues["action"];
        if (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) || HttpMethods.IsOptions(request.Method)
            || context.Controller is PaymentController or StripeWebhookController
            || (context.Controller is EntryController && HttpMethods.IsPost(request.Method)
            && action is nameof(EntryController.AddEntry) or nameof(EntryController.BoostScore)))
        {
            await next();
            return;
        }
        var configured = configuration["Admin:ApiKey"];
        var supplied = request.Headers["X-Admin-Key"].ToString();
        if (string.IsNullOrWhiteSpace(configured) || configured.Length < 32 || supplied.Length > 256
            || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(configured)), SHA256.HashData(Encoding.UTF8.GetBytes(supplied))))
        {
            context.Result = new UnauthorizedResult();
            return;
        }
        await next();
    }
}
