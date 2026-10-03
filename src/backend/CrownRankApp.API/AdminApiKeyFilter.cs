using System.Security.Cryptography;
using System.Text;
using CrownRankApp.API.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CrownRankApp.API;

// Public visitors can start/confirm payments; all other mutations remain administrator-only.
public sealed class AdminApiKeyFilter(IConfiguration configuration) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        if (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) || HttpMethods.IsOptions(request.Method)
            || context.Controller is PaymentController or StripeWebhookController or ProfileImagesController
            || (context.Controller is EntryController && HttpMethods.IsPost(request.Method)
            && context.ActionDescriptor.RouteValues["action"] == nameof(EntryController.AddEntry)))
        {
            await next();
            return;
        }
        var configured = configuration["Admin:ApiKey"];
        var supplied = request.Headers["X-Admin-Key"].ToString();
        if (string.IsNullOrWhiteSpace(configured) || supplied.Length > 256 || !CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(configured)), SHA256.HashData(Encoding.UTF8.GetBytes(supplied))))
        {
            context.Result = new UnauthorizedResult();
            return;
        }
        await next();
    }
}
