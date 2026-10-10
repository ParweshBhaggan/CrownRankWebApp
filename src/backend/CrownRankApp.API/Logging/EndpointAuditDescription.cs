using Microsoft.AspNetCore.Mvc.Controllers;

namespace CrownRankApp.API.Logging;

internal static class EndpointAuditDescription
{
    public static string GetText(ControllerActionDescriptor? action, int statusCode)
    {
        var key = action is null ? string.Empty : $"{action.ControllerName}.{action.ActionName}";
        var text = key switch
        {
            "Entry.GetEntries" => "User viewed the global leaderboard.",
            "Entry.GetEntryById" => "User viewed a creator profile.",
            "Entry.AddEntry" => "User completed a paid creator entry registration.",
            "Entry.BoostScore" => "User applied a paid boost to a creator.",
            "Entry.GetDaily" => "User viewed the daily ranking.",
            "Entry.DeleteEntry" => "Admin deleted a creator entry.",

            "Category.GetCategories" => "User loaded the available categories.",
            "Category.GetCategoryById" => "User viewed a category.",
            "Category.GetCategoryByName" => "User looked up a category.",
            "Category.AddCategory" => "Admin added a category.",
            "Category.UpdateCategory" => "Admin updated a category.",
            "Category.DeleteCategory" => "Admin deleted a category.",

            "SocialMediaDefault.GetSocialMediaDefaults" => "User loaded the default social media options.",
            "SocialMediaDefault.GetSocialMediaDefaultById" => "User viewed a default social media option.",
            "SocialMediaDefault.AddSocialMediaDefault" => "Admin added a default social media option.",
            "SocialMediaDefault.UpdateSocialMediaDefault" => "Admin updated a default social media option.",
            "SocialMediaDefault.DeleteSocialMediaDefault" => "Admin deleted a default social media option.",

            "SocialMediaPlatform.GetSocialMediaPlatforms" => "User loaded social media platforms.",
            "SocialMediaPlatform.GetSocialMediaPlatformById" => "User viewed a social media platform.",
            "SocialMediaPlatform.AddSocialMediaPlatform" => "Admin added a social media platform.",
            "SocialMediaPlatform.DeleteSocialMediaPlatform" => "Admin deleted a social media platform.",

            "Payment.Settings" => "User loaded the payment settings.",
            "Payment.EntryCheckout" => "User started checkout for a new creator entry.",
            "Payment.BoostCheckout" => "User started checkout for a creator boost.",
            "Payment.Confirm" => "User checked whether a payment was completed.",
            "Payment.Resume" => "User resumed an existing checkout.",

            "AdminAuth.Login" when statusCode < 400 => "Admin signed in.",
            "AdminAuth.Login" => "Admin sign-in failed.",
            "StripeWebhook.Receive" => "Stripe sent a payment status update.",
            _ => "An API endpoint was requested."
        };

        if (statusCode >= 400 && key is not "AdminAuth.Login")
        {
            return $"{text.TrimEnd('.')} The request failed.";
        }

        return text;
    }

    public static string GetWho(HttpContext context, ControllerActionDescriptor? action)
    {
        if (action?.ControllerName == "StripeWebhook")
        {
            return "System";
        }

        if (action?.ControllerName == "AdminAuth" || context.User.IsInRole(Authentication.AdminRoles.Admin))
        {
            return "Admin";
        }

        return "User";
    }

    public static string GetEndpoint(HttpContext context, ControllerActionDescriptor? action)
    {
        var template = action?.AttributeRouteInfo?.Template;
        return string.IsNullOrWhiteSpace(template)
            ? $"{context.Request.Method} {context.Request.Path}"
            : $"{context.Request.Method} /{template.TrimStart('/')}";
    }

    public static string GetIds(HttpContext context)
    {
        var ids = new List<string>();

        foreach (var value in context.Request.RouteValues)
        {
            if (IsIdName(value.Key) && value.Value is not null)
            {
                ids.Add($"{value.Key}: {value.Value}");
            }
        }

        foreach (var key in new[] { "paymentId", "referenceId", "entryId", "id" })
        {
            if (!context.Request.Query.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (ids.All(item => !item.StartsWith($"{key}:", StringComparison.OrdinalIgnoreCase)))
            {
                ids.Add($"{key}: {value}");
            }
        }

        return ids.Count == 0 ? "None" : string.Join(", ", ids);
    }

    private static bool IsIdName(string key)
    {
        return key.Equals("id", StringComparison.OrdinalIgnoreCase)
            || key.EndsWith("Id", StringComparison.OrdinalIgnoreCase);
    }
}
