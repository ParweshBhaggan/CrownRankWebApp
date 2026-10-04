using System.Collections.Concurrent;
using CrownRankApp.Application.Dtos.Entry;
using CrownRankApp.Application.Dtos.Category;
using CrownRankApp.Application.Dtos.SocialMedia;
using CrownRankApp.Application.Payments;
using CrownRankApp.Infrastructure.Data;
using CrownRankApp.Infrastructure.Payments;
using CrownRankApp.Infrastructure.Services.Entry;
using Microsoft.EntityFrameworkCore;
using Stripe;

static class PaymentChecks
{
    public static async Task Run(DbContextOptions<ApplicationDbContext> options)
    {
        var gateway = new FakeGateway();
        var settings = new PaymentSettings();
        var stripe = new StripeSettings
        {
            WebhookSecret = "whsec_test"
        };
        async Task<T> With<T>(Func<IPaymentService, Task<T>> action)
        {
            await using var context = new ApplicationDbContext(options);
            var service = new PaymentService(context, new EntryServices(context, TimeProvider.System, settings), gateway, settings, stripe, TimeProvider.System);
            return await action(service);
        }
        void Check(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception($"FAILED: {message}");
            }
            Console.WriteLine($"PASS: {message}");
        }
        async Task Reject<T>(Func<Task> action, string message) where T : Exception
        {
            try
            {
                await action();
            }
            catch (T)
            {
                Check(true, message);
                return;
            }
            throw new Exception($"FAILED: {message}");
        }
        await using var lookup = new ApplicationDbContext(options);
        var category = await lookup.Categories.FirstAsync();
        var platform = await lookup.SocialMediaDefaults.FirstAsync();
        var reference = Guid.NewGuid();
        var checkout = new CheckoutRequest("Recovery creator", 12.50m);
        const string image = "data:image/webp;base64,UklGRhIAAABXRUJQVlA4TAYAAAAvAAAAAAfQ//73v/+BiOh/AAA=";
        var form = new EntryResponseDto
        {
            Name = checkout.Name,
            Username = $"payment_{reference:N}",
            ImgUrl = image,
            Score = 99999m,
            Categories = [new CategoryDto
            {
                Name = category.Name
            }],
            SocialMediaPlatforms = [new SocialMediaPlatformDto
            {
                PlatformName = platform.Name,
                Url = "https://example.com/creator"
            }]
        };
        foreach (var amount in new[]
        {
            0m,
            -1m,
            9.99m,
            10000.01m,
            10.001m
        })
        {
            await Reject<ArgumentException>(() => With(service => service.StartAsync(Guid.NewGuid(), checkout with
                        {
                            Amount = amount
                        }, null, default)), $"Checkout rejects {amount}");
        }
        var starts = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => With(service => service.StartAsync(reference, checkout, null, default))));
        Check(starts.All(value => value.Url == starts[0].Url) && gateway.Created == 1, "Concurrent minimal checkout retries create one Stripe session");
        Check(!await lookup.Entries.AnyAsync(entry => entry.Username == form.Username), "Checkout creates no entry or image record");
        await Reject<InvalidOperationException>(() => With(service => service.StartAsync(reference, checkout with
                    {
                        Amount = 13m
                    }, null, default)), "Changed amount cannot reuse a reference");
        await Reject<InvalidOperationException>(() => With(service => service.RegisterEntryAsync(reference, form, default)), "Unpaid entry registration is rejected");
        var resumed = await With(service => service.ResumeAsync(reference, default));
        Check(resumed.Url == starts[0].Url && gateway.Created == 1, "Cancel/resume reuses the original checkout");
        gateway.Pay(reference);
        var paid = await With(service => service.ConfirmAsync(reference, default));
        Check(paid.Status == "paid" && !paid.Fulfilled, "Payment is recorded independently of the original form");
        await Reject<ArgumentException>(() => With(service => service.RegisterEntryAsync(reference, new EntryResponseDto
                    {
                        Name = "Changed name"
                    }, default)), "Registration checks the paid name");
        var registered = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => With(service => service.RegisterEntryAsync(reference, form, default))));
        Check(registered.Select(entry => entry.Id).Distinct().Count() == 1, "Concurrent paid registrations create one entry");
        var creator = await lookup.Entries.SingleAsync(entry => entry.Id == registered[0].Id);
        Check(creator.ImgUrl == image, "Original image data URL is stored byte-for-byte without new image processing");
        Check(creator.Score == 12.50m && await lookup.ScoreAdditions.CountAsync(value => value.EntryId == creator.Id) == 1,
            "Verified payment amount replaces browser score and is credited once");
        var boostReference = Guid.NewGuid();
        await With(service => service.StartAsync(boostReference, new CheckoutRequest("Creator boost", 10.50m), creator.Id, default));
        await Reject<InvalidOperationException>(() => With(service => service.RegisterBoostAsync(boostReference, creator.Id, default)), "Direct unpaid boost is rejected");
        await Reject<InvalidOperationException>(async () =>
            {
                await using var context = new ApplicationDbContext(options);
                await new EntryServices(context, TimeProvider.System).DeleteAsync(creator.Id);
            }, "Active boost protects its creator from deletion");
        gateway.Pay(boostReference);
        var boosted = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => With(service => service.ConfirmAsync(boostReference, default))));
        Check(boosted.All(value => value.Fulfilled), "Concurrent boost confirmations fulfill safely");
        Check(await lookup.Entries.AsNoTracking().Where(entry => entry.Id == creator.Id).Select(entry => entry.Score).SingleAsync() == 23m
                && await lookup.ScoreAdditions.CountAsync(value => value.Id == boostReference) == 1, "Existing boost service credits each payment once");
        var correctedReference = Guid.NewGuid();
        await With(service => service.StartAsync(correctedReference, checkout, null, default));
        gateway.Pay(correctedReference);
        await Reject<InvalidOperationException>(() => With(service => service.RegisterEntryAsync(correctedReference, form, default)), "Duplicate username leaves the paid payment available for correction");
        form.Username = $"corrected_{correctedReference:N}";
        var corrected = await With(service => service.RegisterEntryAsync(correctedReference, form, default));
        Check(corrected.ImgUrl == image && corrected.Score == 12.50m && gateway.Created == 3, "Corrected paid form keeps the image and never charges again");
        var uncertain = Guid.NewGuid();
        gateway.LoseNextResponse = true;
        await Reject<PaymentUnavailableException>(() => With(service => service.StartAsync(uncertain, checkout, null, default)), "Lost Stripe response leaves a retryable payment reference");
        await With(service => service.ResumeAsync(uncertain, default));
        Check(gateway.Created == 4, "Retry after uncertain checkout creation reuses Stripe idempotency");
        gateway.Expire(uncertain);
        Check((await With(service => service.ConfirmAsync(uncertain, default))).Status == "expired", "Expired unpaid session cannot publish an entry");
        var mismatch = Guid.NewGuid();
        await With(service => service.StartAsync(mismatch, checkout, null, default));
        gateway.ChangeAmount(mismatch);
        await Reject<InvalidOperationException>(() => With(service => service.ConfirmAsync(mismatch, default)), "Confirmation rejects a mismatched Stripe amount");
        var webhookReference = Guid.NewGuid();
        await With(service => service.StartAsync(webhookReference, new CheckoutRequest("Webhook boost", 10m), creator.Id, default));
        gateway.Pay(webhookReference);
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => With(async service =>
                    {
                        await service.HandleWebhookAsync(webhookReference.ToString(), "signed", default);
                        return true;
                    })));
        Check(await lookup.ScoreAdditions.CountAsync(value => value.Id == webhookReference) == 1, "Duplicate signed webhook deliveries credit the existing boost once");
        await Reject<InvalidWebhookException>(() => With(async service =>
                {
                    await service.HandleWebhookAsync("{}", "forged", default);
                    return true;
                }), "Invalid webhook signature is rejected");
        await Reject<InvalidWebhookException>(() => Task.Run(() => new StripePaymentGateway(new StripeClient("sk_test_placeholder"), stripe).VerifyWebhook("{}", "t=1,v1=invalid")),
            "Actual Stripe SDK rejects a forged signature");
        await HardeningChecks.Run(options, gateway, creator.Id);
        await ApiChecks.Run(options);
    }

    internal sealed class FakeGateway : IPaymentGateway
    {
        private readonly ConcurrentDictionary<Guid, VerifiedCheckout> sessions = new();
        private int created;

        public int Created
        {
            get
            {
                return created;
            }
        }

        public bool LoseNextResponse
        {
            get;
            set;
        }

        public Task<VerifiedCheckout> CreateAsync(CheckoutRequest request, CheckoutContext context, CancellationToken ct)
        {
            var candidate = new VerifiedCheckout($"cs_test_{context.ReferenceId:N}", context.ReferenceId, (long)(request.Amount * 100), context.Currency,
                "open", false, false, $"https://checkout.stripe.com/{context.ReferenceId:N}", null);
            if (sessions.TryAdd(context.ReferenceId, candidate))
            {
                Interlocked.Increment(ref created);
            }
            if (LoseNextResponse)
            {
                LoseNextResponse = false;
                throw new PaymentUnavailableException("The checkout response was interrupted.");
            }
            return Task.FromResult(sessions[context.ReferenceId]);
        }

        public Task<VerifiedCheckout?> FindAsync(CheckoutContext context, CancellationToken ct)
        {
            sessions.TryGetValue(context.ReferenceId, out var session);
            return Task.FromResult(session);
        }

        public Task<VerifiedCheckout> RetrieveAsync(string sessionId, CancellationToken ct)
        {
            return Task.FromResult(sessions.Values.Single(value => value.SessionId == sessionId));
        }

        public VerifiedCheckout? VerifyWebhook(string payload, string signature)
        {
            if (signature != "signed" || !Guid.TryParse(payload, out var id))
            {
                throw new InvalidWebhookException();
            }
            return sessions[id];
        }

        public void Pay(Guid id)
        {
            sessions[id] = sessions[id] with
            {
                Paid = true,
                Status = "complete",
                PaidAt = DateTime.UtcNow,
                Url = null
            };
        }

        public void Expire(Guid id)
        {
            sessions[id] = sessions[id] with
            {
                Status = "expired",
                Url = null
            };
        }

        public void ChangeAmount(Guid id)
        {
            sessions[id] = sessions[id] with
            {
                AmountInCents = 1
            };
        }
    }
}
