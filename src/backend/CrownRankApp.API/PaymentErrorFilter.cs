using CrownRankApp.Application.Payments;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CrownRankApp.API;

public sealed class PaymentErrorFilter : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        var status = context.Exception switch
        {
        InvalidWebhookException => 400,
        ArgumentException => 400,
        KeyNotFoundException => 404,
        InvalidOperationException => 409,
        PaymentUnavailableException => 503,
        _ => 0
        };
        if (status == 0)
        {
            return;
        }
        context.Result = new ObjectResult(new ProblemDetails
            {
                Status = status,
                Title = "The payment request could not be completed.",
                Detail = context.Exception.Message
            })
        {
            StatusCode = status
        };
        context.ExceptionHandled = true;
    }
}
