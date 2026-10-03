using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Collections.Concurrent;
using CrownRankApp.Application.Payments;
using CrownRankApp.Domain.Models;
using CrownRankApp.Infrastructure.Data;
using CrownRankApp.Infrastructure.Payments;
using CrownRankApp.Infrastructure.Services.Entry;
using Microsoft.EntityFrameworkCore;
using StripeClient = Stripe.StripeClient;

static class PaymentChecks
{
    public static async Task Run(DbContextOptions<ApplicationDbContext> options, TimeProvider clock)
    {
        var folder = Path.Combine(Path.GetTempPath(), $"crownrank-images-{Guid.NewGuid():N}");
        var images = new ProfileImageStorage(folder);
        var gateway = new FakeGateway(clock);
        var settings = new PaymentSettings();
        settings.Validate();
        async Task<T> With<T>(Func<CheckoutService, Task<T>> action)
        {
            await using var context = new ApplicationDbContext(options);
            return await action(new CheckoutService(new PaymentStore(context, images, clock), gateway, settings, clock));
        }
        void Check(bool condition, string message)
        {
            if (!condition) throw new Exception($"FAILED: {message}");
            Console.WriteLine($"PASS: {message}");
        }
        async Task Reject<T>(Func<Task> action, string message) where T : Exception
        {
            try { await action(); }
            catch (T) { Check(true, message); return; }
            throw new Exception($"FAILED: {message}");
        }
        try
        {
            await using var lookup = new ApplicationDbContext(options);
            var category = await lookup.Categories.FirstAsync();
            var platform = await lookup.SocialMediaDefaults.SingleAsync(platform => platform.Name == "Instagram");
            using var sourceImage = new Image<Rgba32>(600, 500);
            using var imageBytes = new MemoryStream();
            sourceImage.SaveAsPng(imageBytes);
            var png = "data:image/png;base64," + Convert.ToBase64String(imageBytes.ToArray());
            var request = new EntryCheckoutRequest(Guid.NewGuid(), 12.50m, "Paid creator", "paid_creator", category.Id,
                [new SocialProfileRequest(platform.Id, "https://instagram.com/paid_creator")], png, true);
            foreach (var amount in new[] { 0m, -10m, 9.99m, 10000.01m, 10.001m })
                await Reject<ArgumentException>(() => With(service => service.StartEntryAsync(request with { Amount = amount })), $"Checkout rejects {amount}");
            await Reject<ArgumentException>(() => With(service => service.StartEntryAsync(request with { AcceptedAgreements = false })), "Agreement acceptance is enforced on the server");
            var session = await With(service => service.StartEntryAsync(request));
            var storedImage = Image.Identify(Path.Combine(folder, "assets", "profiles", $"{session.Id:N}.webp"));
            Check(storedImage.Width == 300 && storedImage.Height == 250, "Server resizes profile images to the maximum dimensions");
            Check(session.Status == "pending", "New entry remains pending before payment");
            await using (var context = new ApplicationDbContext(options))
                Check(!await context.Entries.AnyAsync(entry => entry.Username == request.Username), "Unpaid entry is absent from the board");
            Check((await With(service => service.StartEntryAsync(request))).Id == session.Id, "Submission retry reuses its operation");
            Check(gateway.Created == 1, "Submission retry does not create another Stripe session");
            await Reject<InvalidOperationException>(() => With(service => service.StartEntryAsync(request with { Amount = 20m })), "Changed details cannot reuse a reference");
            await Reject<InvalidOperationException>(() => With(service => service.StartEntryAsync(request with { ReferenceId = Guid.NewGuid() })), "Username is reserved during checkout");
            gateway.Pay(session.Id);
            await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => With(service => service.ConfirmAsync(session.Id))));
            var paid = (await With(service => service.StatusAsync(session.Id)))!;
            Check(paid.Fulfilled && paid.Status == "paid", "Concurrent confirmations fulfill the entry once");
            await using (var context = new ApplicationDbContext(options))
            {
                Check(await context.ScoreAdditions.CountAsync(row => row.Id == session.Id) == 1, "One opening score exists per paid operation");
                var payment = await context.PaymentOperations.FindAsync(session.Id);
                Check(payment!.AgreementsAcceptedAt != null && payment.TermsVersion == settings.TermsVersion, "Agreement evidence is persisted");
            }
            var boost = new BoostCheckoutRequest(Guid.NewGuid(), paid.EntryId!.Value, 10m);
            var boostSession = await With(service => service.StartBoostAsync(boost));
            await using (var context = new ApplicationDbContext(options))
                Check((await context.Entries.FindAsync(paid.EntryId))!.Score == 12.5m, "Unpaid boost does not change score");
            await Reject<InvalidOperationException>(async () =>
            {
                await using var context = new ApplicationDbContext(options);
                await new EntryServices(context, clock).DeleteAsync(paid.EntryId.Value);
            }, "Active boost prevents deletion of its entry");
            gateway.Pay(boostSession.Id);
            await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => With(service => service.ConfirmAsync(boostSession.Id))));
            await using (var context = new ApplicationDbContext(options))
                Check((await context.Entries.FindAsync(paid.EntryId))!.Score == 22.5m, "Concurrent boost confirmations credit once");

            var independent = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => With(service => service.StartBoostAsync(boost with { ReferenceId = Guid.NewGuid() }))));
            foreach (var checkout in independent) gateway.Pay(checkout.Id);
            await Task.WhenAll(independent.Select(checkout => With(service => service.ConfirmAsync(checkout.Id))));
            await using (var context = new ApplicationDbContext(options))
            {
                Check((await context.Entries.FindAsync(paid.EntryId))!.Score == 102.5m, "Independent paid boosts do not overwrite one another");
                Check(await context.ScoreAdditions.Where(row => row.EntryId == paid.EntryId).SumAsync(row => row.Amount) == 102.5m,
                    "Paid score history equals total score");
            }
            var mismatch = await With(service => service.StartBoostAsync(boost with { ReferenceId = Guid.NewGuid() }));
            gateway.Pay(mismatch.Id);
            gateway.ChangeAmount(mismatch.Id);
            await Reject<InvalidOperationException>(() => With(service => service.ConfirmAsync(mismatch.Id)), "Payment amount mismatch is rejected");
            var expiredRequest = request with { ReferenceId = Guid.NewGuid(), Username = "expires_creator" };
            var expired = await With(service => service.StartEntryAsync(expiredRequest));
            gateway.Expire(expired.Id);
            Check((await With(service => service.ConfirmAsync(expired.Id)))!.Status == "expired", "Stripe expiry is recorded");
            await With(service => service.StartEntryAsync(expiredRequest with { ReferenceId = Guid.NewGuid() }));
            Check(true, "Expired checkout releases its username reservation");
            var recoveryBoost = await With(service => service.StartBoostAsync(boost with { ReferenceId = Guid.NewGuid() }));
            gateway.Pay(recoveryBoost.Id);
            await using (var context = new ApplicationDbContext(options))
                await context.PaymentOperations.Where(operation => operation.Id == recoveryBoost.Id)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(operation => operation.SessionId, (string?)null)
                        .SetProperty(operation => operation.ExpiresAt, clock.GetUtcNow().UtcDateTime.AddMinutes(-1)));
            Check((await With(service => service.ResumeAsync(recoveryBoost.Id)))!.Status == "paid", "Lost session attachment is recovered without a new charge");

            var failedRequest = boost with { ReferenceId = Guid.NewGuid() };
            var failedCheckout = await With(service => service.StartBoostAsync(failedRequest));
            gateway.Fail(failedCheckout.Id);
            Check((await With(service => service.ConfirmAsync(failedCheckout.Id)))!.Status == "failed", "Verified payment failure does not award a score");

            var recoveryEntryId = Guid.NewGuid();
            await using (var context = new ApplicationDbContext(options))
            {
                context.Entries.Add(new Entry(recoveryEntryId) { Name = "Recovery", Username = "recovery", Score = 10m });
                await context.SaveChangesAsync();
            }
            var recoverable = await With(service => service.StartBoostAsync(new BoostCheckoutRequest(Guid.NewGuid(), recoveryEntryId, 10m)));
            gateway.Pay(recoverable.Id);
            await using (var context = new ApplicationDbContext(options))
            {
                context.Entries.Remove((await context.Entries.FindAsync(recoveryEntryId))!);
                await context.SaveChangesAsync();
            }
            await Reject<InvalidOperationException>(() => With(service => service.ConfirmAsync(recoverable.Id)), "Fulfillment failure is surfaced after verified payment");
            var failedFulfillment = (await With(service => service.StatusAsync(recoverable.Id)))!;
            Check(failedFulfillment.Status == "paid" && !failedFulfillment.Fulfilled, "Payment remains paid with fulfillment pending after rollback");
            await using (var context = new ApplicationDbContext(options))
            {
                context.Entries.Add(new Entry(recoveryEntryId) { Name = "Recovery", Username = "recovery", Score = 10m });
                await context.SaveChangesAsync();
            }
            Check((await With(service => service.ConfirmAsync(recoverable.Id)))!.Fulfilled, "Retry recovers failed fulfillment without charging again");

            var stripe = new StripePaymentGateway(new StripeClient("sk_test_example"), new StripeSettings { WebhookSecret = "whsec_example" });
            await Reject<InvalidWebhookException>(() => Task.Run(() => stripe.VerifyWebhook("{}", "t=1,v1=invalid")), "Forged webhook signature is rejected");
            var configured = new PaymentSettings { MinimumAmount = 25m, MaximumAmount = 50m };
            configured.Validate();
            await Reject<ArgumentException>(() => Task.Run(() => configured.ValidateAmount(10m)), "Changed configuration controls backend limits");
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }

    private sealed class FakeGateway(TimeProvider clock) : IPaymentGateway
    {
        private readonly ConcurrentDictionary<Guid, VerifiedCheckout> sessions = new();
        public int Created;
        public Task<VerifiedCheckout> CreateAsync(PaymentOperation operation, CancellationToken ct)
        {
            var session = sessions.GetOrAdd(operation.Id, id =>
            {
                Interlocked.Increment(ref Created);
                return new VerifiedCheckout($"cs_{id:N}", id, (long)(operation.Amount * 100), operation.Currency,
                    "open", false, null, null, "https://checkout.stripe.com/test", false);
            });
            return Task.FromResult(session);
        }
        public Task<VerifiedCheckout?> FindAsync(PaymentOperation operation, CancellationToken ct) => Task.FromResult(sessions.GetValueOrDefault(operation.Id));
        public Task<VerifiedCheckout> RetrieveAsync(string sessionId, CancellationToken ct) => Task.FromResult(sessions.Values.Single(session => session.SessionId == sessionId));
        public VerifiedCheckout? VerifyWebhook(string payload, string signature) => sessions[Guid.Parse(payload)];
        public void Pay(Guid id) => sessions[id] = sessions[id] with { Paid = true, Status = "complete", PaymentIntentId = $"pi_{id:N}", PaidAt = clock.GetUtcNow().UtcDateTime, Url = null };
        public void Expire(Guid id) => sessions[id] = sessions[id] with { Status = "expired", Url = null };
        public void Fail(Guid id) => sessions[id] = sessions[id] with { Status = "complete", Failed = true };
        public void ChangeAmount(Guid id) => sessions[id] = sessions[id] with { AmountMinor = 1 };
    }
}
