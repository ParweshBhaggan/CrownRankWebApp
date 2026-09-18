using CrownRank.Application.Abstractions;
using CrownRank.Application.Contracts;
using CrownRank.Application.Services;
using CrownRank.Domain.Models;
using CrownRank.Domain.ValueObjects;
using CrownRank.Infrastructure.Payments;
using Microsoft.Extensions.DependencyInjection;

namespace CrownRank.ConsoleApp;

public sealed class ConsoleRunner(IServiceProvider services, TextReader input, TextWriter output)
{
    private sealed class InputClosedException : Exception { }

    public async Task RunAsync(CancellationToken ct = default)
    {
        output.WriteLine("CrownRank — local mock-payment console (EUR, minor units/cents)");
        output.WriteLine("Development data is seeded automatically for categories, creators and rankings.");
        try
        {
            while (!ct.IsCancellationRequested)
            {
                output.WriteLine("\nPUBLIC: 1 Categories  2 Global/category leaderboard  3 Daily/historical leaderboard");
                output.WriteLine("        4 Profile  5 Submit entry  6 Boost entry  7 Payment status/complete  8 Admin  0 Exit");
                var choice = Ask("Choose");
                if (choice == "0") return;
                if (choice == "8") { await AdminMenuAsync(ct); continue; }
                await SafelyAsync(async sp =>
                {
                    switch (choice)
                    {
                        case "1": await CategoriesAsync(sp, ct); break;
                        case "2": await LeaderboardAsync(sp, false, ct); break;
                        case "3": await LeaderboardAsync(sp, true, ct); break;
                        case "4": await ProfileAsync(sp, ct); break;
                        case "5": await SubmitAsync(sp, ct); break;
                        case "6": await BoostAsync(sp, ct); break;
                        case "7": await PaymentAsync(sp, GuidInput("Attempt ID"), ct); break;
                        default: output.WriteLine("Unknown menu option."); break;
                    }
                });
            }
        }
        catch (InputClosedException) { output.WriteLine("Input closed. Goodbye."); }
    }

    private async Task AdminMenuAsync(CancellationToken ct)
    {
        var credential = Ask("Admin password");
        using (var loginScope = services.CreateScope())
        {
            if (!await loginScope.ServiceProvider.GetRequiredService<IAdminAuthorization>().IsAuthorizedAsync(credential, ct))
            { output.WriteLine("Access denied."); return; }
        }
        output.WriteLine("Admin signed in for this console session.");
        while (!ct.IsCancellationRequested)
        {
            output.WriteLine("\nADMIN: 1 List entries  2 List categories  3 Payments  4 Add category  5 Edit category");
            output.WriteLine("       6 Archive category  7 Edit entry  8 Hide entry  9 Restore entry  10 Archive entry  0 Sign out");
            var choice = Ask("Choose");
            if (choice == "0") return;
            await SafelyAsync(async sp =>
            {
                var inspection = sp.GetRequiredService<AdminInspectionService>();
                var categories = sp.GetRequiredService<AdminCategoryService>();
                var entries = sp.GetRequiredService<AdminEntryService>();
                switch (choice)
                {
                    case "1":
                        foreach (var e in await inspection.EntriesAsync(ct))
                            output.WriteLine($"{e.Id} | {e.Name} @{e.Username} | {e.Status} | category {e.CategoryId}");
                        break;
                    case "2":
                        foreach (var c in await inspection.CategoriesAsync(ct))
                            output.WriteLine($"{c.Id} | {c.Name} | {c.Status} | {c.Description}");
                        break;
                    case "3":
                        foreach (var p in await inspection.PaymentsAsync(ct))
                            output.WriteLine($"{p.Id} | entry {p.EntryId} | {p.Purpose} {p.State} | {Format(p.AmountInMinorUnits, p.Currency)} | {p.Provider}:{p.Reference}");
                        break;
                    case "4":
                        var created = await categories.CreateAsync(Ask("Category name"), Ask("Description (optional)"), ct);
                        output.WriteLine($"Category saved: {created.Id}");
                        break;
                    case "5":
                        await categories.UpdateAsync(GuidInput("Category ID"), Ask("New name"), Ask("New description"), ct);
                        output.WriteLine("Category saved."); break;
                    case "6":
                        await categories.ArchiveAsync(GuidInput("Category ID"), ct);
                        output.WriteLine("Category archived."); break;
                    case "7": await EditEntryAsync(entries, ct); break;
                    case "8": await entries.HideAsync(GuidInput("Entry ID"), ct);
                        output.WriteLine("Entry hidden."); break;
                    case "9": await entries.RestoreAsync(GuidInput("Entry ID"), ct);
                        output.WriteLine("Entry restored."); break;
                    case "10": await entries.ArchiveAsync(GuidInput("Entry ID"), ct);
                        output.WriteLine("Entry archived."); break;
                    default: output.WriteLine("Unknown menu option."); break;
                }
            });
        }
    }

    private async Task EditEntryAsync(AdminEntryService entries, CancellationToken ct)
    {
        var id = GuidInput("Entry ID");
        output.WriteLine("Edit: 1 Name/username  2 Category  3 Social accounts  4 Profile image");
        switch (Ask("Choose"))
        {
            case "1": await entries.UpdateDetailsAsync(id, Ask("Name"), Ask("Username"), ct); break;
            case "2": await entries.ChangeCategoryAsync(id, GuidInput("New category ID"), ct); break;
            case "3": await entries.ReplaceSocialLinksAsync(id, Links(), ct); break;
            case "4":
                var path = Ask("Image path (PNG/JPEG/WebP, up to 5 MB)");
                await using (var image = File.OpenRead(path))
                    await entries.ReplaceImageAsync(id, image, path, ct);
                break;
            default: output.WriteLine("No changes made."); return;
        }
        output.WriteLine("Entry saved.");
    }

    private async Task CategoriesAsync(IServiceProvider sp, CancellationToken ct)
    {
        var rows = await sp.GetRequiredService<PublicReadService>().CategoriesAsync(ct);
        if (rows.Count == 0) output.WriteLine("No active categories. Admin can create one.");
        foreach (var c in rows) output.WriteLine($"{c.Id} | {c.Name} | {c.Description}");
    }

    private async Task LeaderboardAsync(IServiceProvider sp, bool daily, CancellationToken ct)
    {
        var category = OptionalGuid("Category ID (blank = all)");
        var day = daily ? DateOnly.ParseExact(Ask("UTC date (yyyy-MM-dd)"), "yyyy-MM-dd") : default;
        var read = sp.GetRequiredService<PublicReadService>();
        var rows = daily ? await read.DailyAsync(day, category, ct) : await read.GlobalAsync(category, ct);
        if (rows.Count == 0) output.WriteLine("No ranked entries for this selection.");
        foreach (var row in rows)
            output.WriteLine($"#{row.Rank} {row.Name} @{row.Username} | {Format(row.ScoreInMinorUnits, row.Currency)} | entry {row.EntryId}");
    }

    private async Task ProfileAsync(IServiceProvider sp, CancellationToken ct)
    {
        var entry = await sp.GetRequiredService<PublicReadService>().ProfileAsync(GuidInput("Entry ID"), ct);
        if (entry is null) { output.WriteLine("Public profile not found."); return; }
        output.WriteLine($"{entry.Name} @{entry.Username} | category {entry.CategoryId} | image key {entry.ProfileImageKey}");
        foreach (var link in entry.SocialMediaLinks)
            output.WriteLine($"  {link.CustomPlatformName ?? link.Platform.ToString()}: {link.Url}");
    }

    private async Task SubmitAsync(IServiceProvider sp, CancellationToken ct)
    {
        var categories = await sp.GetRequiredService<PublicReadService>().CategoriesAsync(ct);
        if (categories.Count == 0) { output.WriteLine("No category available; admin must create one first."); return; }
        for (var i = 0; i < categories.Count; i++) output.WriteLine($"{i + 1}. {categories[i].Name} ({categories[i].Id})");
        var selected = Number("Category number", 1, categories.Count) - 1;
        var name = Ask("Profile name");
        var username = Ask("Username/handle");
        var links = Links();
        var path = Ask("Profile image path (PNG/JPEG/WebP, up to 5 MB)");
        var amount = Amount("Entry payment in EUR cents (e.g. 1000 = EUR 10.00)");
        var versions = await sp.GetRequiredService<ILegalDocumentVersions>().CurrentAsync(ct);
        output.WriteLine($"Accept Terms {versions.Terms}, Privacy {versions.Privacy} and Rules {versions.Rules}? (yes/no)");
        if (!Yes(Ask("Acceptance"))) { output.WriteLine("Submission cancelled: agreement required."); return; }
        output.WriteLine($"Review: {name} @{username}, {categories[selected].Name}, {links.Count} link(s), {Format(amount, "EUR")}. Start mock checkout? (yes/no)");
        if (!Yes(Ask("Confirm"))) { output.WriteLine("Submission cancelled."); return; }
        await using var image = File.OpenRead(path);
        var started = await sp.GetRequiredService<EntrySubmissionService>().SubmitAsync(new SubmitEntryRequest(
            name, username, categories[selected].Id, image, path, links, true, amount, "EUR"), ct);
        output.WriteLine($"Entry saved pending payment: {started.EntryId}. Attempt: {started.AttemptId}.");
        await CompleteMockAsync(sp, started.AttemptId, ct);
    }

    private async Task BoostAsync(IServiceProvider sp, CancellationToken ct)
    {
        var entryId = GuidInput("Published entry ID to boost (owner or fan)");
        var read = sp.GetRequiredService<PublicReadService>();
        var entry = await read.ProfileAsync(entryId, ct) ?? throw new KeyNotFoundException("Public entry not found.");
        var amount = Amount("Boost in EUR cents");
        var current = (await read.GlobalAsync(entry.CategoryId, ct)).FirstOrDefault(x => x.EntryId == entryId);
        if (current is null) throw new InvalidOperationException("Entry is not in the leaderboard.");
        output.WriteLine($"Review: boost {entry.Name} @{entry.Username}, {Format(amount, "EUR")}. Current score {Format(current.ScoreInMinorUnits, current.Currency)}, projected score {Format(checked(current.ScoreInMinorUnits + amount), current.Currency)}. Actual rank may change. Continue? (yes/no)");
        if (!Yes(Ask("Confirm"))) { output.WriteLine("Boost cancelled."); return; }
        var started = await sp.GetRequiredService<PaymentService>().StartBoostAsync(entryId, Money.Create(amount, "EUR"), ct);
        output.WriteLine($"Boost awaiting payment. Attempt: {started.AttemptId}.");
        await CompleteMockAsync(sp, started.AttemptId, ct);
    }

    private async Task PaymentAsync(IServiceProvider sp, Guid id, CancellationToken ct)
    {
        var payments = sp.GetRequiredService<PaymentService>();
        var attempt = await payments.GetStatusAsync(id, ct);
        output.WriteLine($"Attempt {id}: {attempt.Purpose}, {attempt.State}, {Format(attempt.ExpectedAmount.AmountInMinorUnits, attempt.ExpectedAmount.Currency)} for entry {attempt.EntryId}");
        if (attempt.State == CrownRank.Application.Payments.PaymentState.CheckoutReady)
            await CompleteMockAsync(sp, id, ct);
    }

    private async Task CompleteMockAsync(IServiceProvider sp, Guid id, CancellationToken ct)
    {
        var gateway = sp.GetRequiredService<MockPaymentGateway>();
        output.WriteLine("Mock checkout: 1 Succeed  2 Keep processing  3 Fail  4 Cancel");
        var outcome = Ask("Choose") switch
        {
            "1" => MockPaymentGateway.MockOutcome.Succeeded,
            "3" => MockPaymentGateway.MockOutcome.Failed,
            "4" => MockPaymentGateway.MockOutcome.Cancelled,
            _ => MockPaymentGateway.MockOutcome.Processing
        };
        gateway.SetOutcome(id, outcome);
        var contribution = await sp.GetRequiredService<PaymentService>().ConfirmAsync(id, ct);
        var status = await sp.GetRequiredService<PaymentService>().GetStatusAsync(id, ct);
        output.WriteLine($"Payment status: {status.State}.");
        if (contribution is not null) output.WriteLine($"Confirmed exactly once. Contribution {contribution.Id}; entry {contribution.EntryId} is ranked.");
        if (status.State == CrownRank.Application.Payments.PaymentState.CheckoutReady)
            output.WriteLine($"Use public option 7 with attempt {id} to check again while this console remains open.");
    }

    private List<SocialLinkInput> Links()
    {
        var count = Number("Number of social accounts (1-10)", 1, 10);
        var links = new List<SocialLinkInput>();
        for (var i = 0; i < count; i++)
        {
            output.WriteLine("Platforms: 1 Instagram 2 TikTok 3 YouTube 4 OnlyFans 5 Facebook 6 X 7 Twitch 8 Website 9 Other");
            var platform = (SocialMediaPlatform)Number("Platform number", 1, 9);
            var custom = platform == SocialMediaPlatform.Other ? Ask("Custom platform name") : null;
            links.Add(new SocialLinkInput(platform, Ask("HTTPS URL"), custom));
        }
        return links;
    }

    private async Task SafelyAsync(Func<IServiceProvider, Task> action)
    {
        using var scope = services.CreateScope();
        try { await action(scope.ServiceProvider); }
        catch (InputClosedException) { throw; }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException
                                   or UnauthorizedAccessException or IOException or OverflowException or FormatException)
        { output.WriteLine($"Could not complete action: {ex.Message}"); }
    }

    private string Ask(string label)
    {
        output.Write($"{label}: ");
        return input.ReadLine()?.Trim() ?? throw new InputClosedException();
    }
    private Guid GuidInput(string label) => Guid.Parse(Ask(label));
    private Guid? OptionalGuid(string label) => Ask(label) is var s && s.Length > 0 ? Guid.Parse(s) : null;
    private int Number(string label, int min, int max)
    {
        if (!int.TryParse(Ask(label), out var n) || n < min || n > max)
            throw new ArgumentException($"Enter a number from {min} to {max}.");
        return n;
    }
    private long Amount(string label)
    {
        if (!long.TryParse(Ask(label), out var n) || n <= 0) throw new ArgumentException("Enter a positive whole number of cents.");
        return n;
    }
    private static bool Yes(string answer) => answer.Equals("yes", StringComparison.OrdinalIgnoreCase);
    private static string Format(long cents, string currency) => $"{cents / 100}.{cents % 100:00} {currency}";
}
