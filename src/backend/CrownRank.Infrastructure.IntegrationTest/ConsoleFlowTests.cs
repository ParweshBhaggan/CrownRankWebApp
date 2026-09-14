using CrownRank.Application.Abstractions;
using CrownRank.Application.Services;
using CrownRank.ConsoleApp;
using CrownRank.Domain.Models;
using CrownRank.Infrastructure.Configuration;
using CrownRank.Infrastructure.Payments;
using CrownRank.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace CrownRank.Infrastructure.IntegrationTest;

public sealed class ConsoleFlowTests
{
    [Fact]
    public async Task Mock_checkout_can_be_verified_after_restarting_the_gateway()
    {
        var directory = Path.Combine(Path.GetTempPath(), "crownrank-mock-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var file = Path.Combine(directory, "mock.json");
            var attempt = Guid.NewGuid();
            var original = new MockPaymentGateway(file);
            await original.CreateCheckoutAsync(attempt, CrownRank.Domain.ValueObjects.Money.Create(250, "EUR"));
            original.SetOutcome(attempt, MockPaymentGateway.MockOutcome.Succeeded);
            var restarted = new MockPaymentGateway(file);
            var verified = await restarted.VerifyAsync("mock", attempt.ToString("N"));
            Assert.True(verified.Succeeded);
            Assert.Equal(250, verified.Amount.AmountInMinorUnits);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task Menu_session_submits_publishes_boosts_and_protects_admin_actions()
    {
        var directory = Path.Combine(Path.GetTempPath(), "crownrank-console-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var imagePath = Path.Combine(directory, "input.png");
            using (var image = new Image<Rgba32>(600, 400)) await image.SaveAsPngAsync(imagePath);
            var services = new ServiceCollection();
            services.AddCrownRankSqlite($"Data Source={Path.Combine(directory, "test.db")}", Path.Combine(directory, "assets"), true);
            services.AddSingleton<ILegalDocumentVersions>(new ConfiguredLegalVersions(new LegalVersions("terms-1", "privacy-1", "rules-1")));
            services.AddSingleton<IAdminAuthorization, ConsoleAdminAuthorization>();
            services.AddScoped<PaymentService>();
            services.AddScoped<EntrySubmissionService>();
            services.AddScoped<PublicReadService>();
            services.AddScoped<AdminEntryService>();
            services.AddScoped<AdminCategoryService>();
            services.AddScoped<AdminInspectionService>();
            using var provider = services.BuildServiceProvider();
            await provider.MigrateCrownRankAsync();

            var admin = await ScriptAsync(provider, "8\nwrong\n8\nconsole-admin\n4\nCreators\nTest category\n0\n0\n");
            Assert.Contains("Access denied", admin);
            Assert.Contains("Category saved", admin);
            Guid categoryId;
            using (var scope = provider.CreateScope())
                categoryId = (await scope.ServiceProvider.GetRequiredService<CrownRankDbContext>().Categories.SingleAsync()).Id;
            var created = await ScriptAsync(provider, $"5\n1\nCreator\ncreator\n1\n1\nhttps://instagram.com/creator\n{imagePath}\n500\nyes\nyes\n1\n0\n");
            Assert.Contains("Payment status: Confirmed", created);
            Guid entryId;
            using (var scope = provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<CrownRankDbContext>();
                var entry = await db.Entries.SingleAsync();
                entryId = entry.Id;
                Assert.Equal(EntryStatus.Published, entry.Status);
                Assert.Equal("terms-1", entry.AgreementAcceptance.TermsVersion);
                Assert.Single(entry.SocialMediaLinks);
                Assert.Single(await db.Contributions.ToListAsync());
            }
            var key = Directory.GetFiles(Path.Combine(directory, "assets"), "*.png").Single();
            using (var stored = await Image.LoadAsync(key))
            {
                Assert.True(stored.Width <= 300);
                Assert.True(stored.Height <= 250);
            }
            var boosted = await ScriptAsync(provider, $"6\n{entryId}\n200\nyes\n1\n2\n\n0\n");
            Assert.Contains("projected score 7.00 EUR", boosted);
            Assert.Contains("#1 Creator", boosted);
            var second = await ScriptAsync(provider, $"5\n1\nAnother\nanother\n2\n2\nhttps://tiktok.com/@another\n3\nhttps://youtube.com/@another\n{imagePath}\n300\nyes\nyes\n2\n0\n");
            Assert.Contains("Payment status: CheckoutReady", second);
            Guid pendingAttempt;
            using (var scope = provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<CrownRankDbContext>();
                pendingAttempt = (await db.PaymentAttempts.SingleAsync(x => x.EntryId != entryId)).Id;
                Assert.Equal(EntryStatus.PendingPayment, (await db.Entries.SingleAsync(x => x.Id != entryId)).Status);
            }
            var completed = await ScriptAsync(provider, $"7\n{pendingAttempt}\n1\n3\n\n{DateTimeOffset.UtcNow:yyyy-MM-dd}\n0\n");
            Assert.Contains("Payment status: Confirmed", completed);
            Assert.Contains("Another", completed);
            using (var scope = provider.CreateScope())
            {
                var rows = await scope.ServiceProvider.GetRequiredService<PublicReadService>().GlobalAsync();
                Assert.Equal(2, rows.Count);
                Assert.Equal(700, rows[0].ScoreInMinorUnits);
                Assert.Equal(300, rows[1].ScoreInMinorUnits);
                Assert.Equal(3, await scope.ServiceProvider.GetRequiredService<CrownRankDbContext>().Contributions.CountAsync());
            }
            var hidden = await ScriptAsync(provider, $"8\nconsole-admin\n8\n{entryId}\n0\n2\n\n0\n");
            Assert.Contains("Entry hidden", hidden);
            Assert.DoesNotContain($"entry {entryId}", hidden);
            Assert.Contains("#1 Another", hidden);
            var restored = await ScriptAsync(provider, $"8\nconsole-admin\n9\n{entryId}\n0\n2\n\n0\n");
            Assert.Contains("Entry restored", restored);
            Assert.Contains("#1 Creator", restored);
            var edit = await ScriptAsync(provider, $"8\nconsole-admin\n7\n{entryId}\n3\n1\n9\nCreator page\nhttps://example.com/creator\n0\n4\n{entryId}\n0\n");
            Assert.Contains("Entry saved", edit);
            Assert.Contains("Creator page", edit);
            var prevented = await ScriptAsync(provider, $"8\nconsole-admin\n6\n{categoryId}\n0\n0\n");
            Assert.Contains("Move or archive the category's entries", prevented);
            var otherId = (await ScriptEntryIdsAsync(provider)).Single(x => x != entryId);
            var archived = await ScriptAsync(provider, $"8\nconsole-admin\n10\n{entryId}\n10\n{otherId}\n6\n{categoryId}\n0\n1\n2\n\n0\n");
            Assert.Contains("Category archived", archived);
            Assert.Contains("No active categories", archived);
            Assert.Contains("No ranked entries", archived);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task Mock_failure_and_cancellation_do_not_publish_an_entry()
    {
        var directory = Path.Combine(Path.GetTempPath(), "crownrank-failed-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var services = new ServiceCollection();
            services.AddCrownRankSqlite($"Data Source={Path.Combine(directory, "test.db")}", Path.Combine(directory, "assets"), true);
            services.AddScoped<PaymentService>();
            using var provider = services.BuildServiceProvider();
            await provider.MigrateCrownRankAsync();
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CrownRankDbContext>();
            var category = Category.Create("Creators", null, DateTimeOffset.UtcNow);
            var entry = Entry.Create("Creator", "creator", category, "test-image.png",
                CrownRank.Domain.ValueObjects.AgreementAcceptance.Create("1", "1", "1", DateTimeOffset.UtcNow),
                [SocialMediaLink.Create(SocialMediaPlatform.Instagram, "https://instagram.com/creator")], DateTimeOffset.UtcNow);
            db.Categories.Add(category);
            db.Entries.Add(entry);
            await db.SaveChangesAsync();
            var payments = scope.ServiceProvider.GetRequiredService<PaymentService>();
            var gateway = provider.GetRequiredService<MockPaymentGateway>();
            foreach (var outcome in new[] { MockPaymentGateway.MockOutcome.Processing,
                         MockPaymentGateway.MockOutcome.Failed, MockPaymentGateway.MockOutcome.Cancelled })
            {
                var started = await payments.StartAsync(entry.Id, CrownRank.Domain.ValueObjects.Money.Create(100, "EUR"),
                    CrownRank.Application.Payments.PaymentPurpose.InitialEntry);
                gateway.SetOutcome(started.AttemptId, outcome);
                Assert.Null(await payments.ConfirmAsync(started.AttemptId));
                var expected = outcome switch
                {
                    MockPaymentGateway.MockOutcome.Failed => CrownRank.Application.Payments.PaymentState.Failed,
                    MockPaymentGateway.MockOutcome.Cancelled => CrownRank.Application.Payments.PaymentState.Cancelled,
                    _ => CrownRank.Application.Payments.PaymentState.CheckoutReady
                };
                Assert.Equal(expected, (await payments.GetStatusAsync(started.AttemptId)).State);
            }
            Assert.Empty(await db.Contributions.ToListAsync());
            Assert.Equal(EntryStatus.PendingPayment, entry.Status);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static async Task<string> ScriptAsync(IServiceProvider provider, string script)
    {
        using var writer = new StringWriter();
        await new ConsoleRunner(provider, new StringReader(script), writer).RunAsync();
        return writer.ToString();
    }

    private static async Task<Guid[]> ScriptEntryIdsAsync(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<CrownRankDbContext>().Entries.Select(x => x.Id).ToArrayAsync();
    }
}
