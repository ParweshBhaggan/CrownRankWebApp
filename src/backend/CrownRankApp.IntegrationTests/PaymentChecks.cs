using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Collections.Concurrent;
using CrownRankApp.Application.Payments;
using CrownRankApp.Application.Dtos.Category;
using CrownRankApp.Application.Dtos.SocialMedia;
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
        try
        {
            await using var lookup = new ApplicationDbContext(options);
            var category = await lookup.Categories.FirstAsync();
            var platform = await lookup.SocialMediaDefaults.SingleAsync(platform => platform.Name == "Instagram");
            using var sourceImage = new Image<Rgba32>(600, 500);
            using var imageBytes = new MemoryStream();
            sourceImage.SaveAsWebp(imageBytes);
            var png = "data:image/webp;base64," + Convert.ToBase64String(imageBytes.ToArray());
            var referenceId = Guid.NewGuid();
            var request = new CheckoutRequest("Paid creator", 12.50m);
            var registration = new EntryRegistrationRequest(request.Name, "paid_creator", png, 99999m,
                [new CategoryDto
                {
                    Name = category.Name
                }], [new SocialMediaPlatformDto
                {
                    PlatformName = platform.Name,
                    Url = "https://instagram.com/paid_creator"
                }], true);
            foreach (var amount in new[]
            {
                0m,
                -10m,
                9.99m,
                10000.01m,
                10.001m
            })
            {
                await Reject<ArgumentException>(() => With(service => service.StartEntryAsync(Guid.NewGuid(), request with
                            {
                                Amount = amount
                            })), $"Checkout rejects {amount}");
            }
            var session = await With(service => service.StartEntryAsync(referenceId, request));
            Check(session.Status == "pending", "Name and amount alone start entry checkout");
            Check(!Directory.Exists(Path.Combine(folder, "assets")), "Checkout does not upload or store a profile image");
            await using (var context = new ApplicationDbContext(options))
            {
                Check(!await context.Entries.AnyAsync(entry => entry.Id == referenceId), "Unpaid entry is absent from the board");
                Check((await context.PaymentOperations.FindAsync(referenceId))!.EntryJson == null, "Checkout stores no entry form payload");
            }
            await Reject<InvalidOperationException>(() => With(service => service.RegisterEntryAsync(referenceId, registration)), "Unpaid registration is rejected");
            Check((await With(service => service.StartEntryAsync(referenceId, request))).Id == session.Id, "Checkout retry reuses its operation");
            Check(gateway.Created == 1, "Checkout retry does not create another Stripe session");
            await Reject<InvalidOperationException>(() => With(service => service.StartEntryAsync(referenceId, request with
                        {
                            Amount = 20m
                        })), "Changed amount cannot reuse a checkout reference");
            await Reject<InvalidOperationException>(() => With(service => service.StartEntryAsync(referenceId, request with
                        {
                            Name = "Another name"
                        })), "Changed name cannot reuse a checkout reference");
            gateway.Pay(session.Id);
            await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => With(service => service.ConfirmAsync(session.Id))));
            var received = (await With(service => service.StatusAsync(session.Id)))!;
            Check(received.Status == "paid" && !received.Fulfilled, "Payment is recorded before the browser registers its form");
            await using (var context = new ApplicationDbContext(options))
            {
                Check(!await context.Entries.AnyAsync(entry => entry.Id == referenceId), "A paid checkout alone does not fabricate an incomplete entry");
            }
            await Reject<ArgumentException>(() => With(service => service.RegisterEntryAsync(referenceId, registration with
                        {
                            AcceptedAgreements = false
                        })), "Registration enforces agreement acceptance");
            await Reject<ArgumentException>(() => With(service => service.RegisterEntryAsync(referenceId, registration with
                        {
                            Name = "Another name"
                        })), "Registered name must match the paid checkout");
            await Reject<ArgumentException>(() => With(service => service.RegisterEntryAsync(referenceId, registration with
                        {
                            ImgUrl = "data:image/png;base64,invalid"
                        })), "Invalid profile images do not consume the payment");
            await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => With(service => service.RegisterEntryAsync(referenceId, registration))));
            var paid = (await With(service => service.StatusAsync(session.Id)))!;
            Check(paid.Fulfilled && paid.Status == "paid", "Concurrent registrations create the paid entry once");
            var storedImage = Image.Identify(Path.Combine(folder, "assets", "profiles", $"{session.Id:N}.webp"));
            Check(storedImage.Width == 300 && storedImage.Height == 250, "Registration saves a validated and resized image");
            await using (var context = new ApplicationDbContext(options))
            {
                Check(await context.ScoreAdditions.CountAsync(row => row.Id == session.Id) == 1, "One opening score exists per paid operation");
                Check((await context.Entries.FindAsync(referenceId))!.Score == request.Amount, "Entry score comes from the verified amount");
                var payment = await context.PaymentOperations.FindAsync(session.Id);
                Check(payment!.AgreementsAcceptedAt != null && payment.TermsVersion == settings.TermsVersion, "Agreement evidence is persisted at registration");
            }
            await With(service => service.RegisterEntryAsync(referenceId, registration with
                    {
                        Username = "ignored_retry"
                    }));
            Check(gateway.Created == 1, "Registration retries never charge again");
            var collision = await With(service => service.StartEntryAsync(Guid.NewGuid(), request));
            gateway.Pay(collision.Id);
            await Reject<InvalidOperationException>(() => With(service => service.RegisterEntryAsync(collision.Id, registration)), "A taken username leaves its payment available for correction");
            Check((await With(service => service.StatusAsync(collision.Id)))!.Status == "paid", "Validation failure preserves verified payment");
            Check((await With(service => service.RegisterEntryAsync(collision.Id, registration with
                        {
                            Username = "corrected_creator"
                        })))!.Fulfilled, "Corrected registration reuses the existing paid checkout");
            var competing = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => With(service => service.StartEntryAsync(Guid.NewGuid(), request))));
            foreach (var checkout in competing)
            {
                gateway.Pay(checkout.Id);
            }
            async Task<PaymentResponse?> RegisterSafely(Guid paymentId)
            {
                try
                {
                    return await With(service => service.RegisterEntryAsync(paymentId, registration with
                            {
                                Username = "race_creator"
                            }));
                }
                catch (InvalidOperationException)
                {
                    return null;
                }
            }
            var registrations = await Task.WhenAll(competing.Select(checkout => RegisterSafely(checkout.Id)));
            Check(registrations.Count(result => result?.Fulfilled == true) == 1, "Concurrent paid registrations cannot take the same username");
            var retryId = competing[Array.FindIndex(registrations, result => result is null)].Id;
            Check((await With(service => service.RegisterEntryAsync(retryId, registration with
                        {
                            Username = "race_corrected"
                        })))!.Fulfilled,
                "A username race rolls back the profile and permits correction without payment reuse");
            var boost = new BoostCheckoutRequest(Guid.NewGuid(), paid.EntryId!.Value, 10m);
            var boostSession = await With(service => service.StartBoostAsync(boost));
            await using (var context = new ApplicationDbContext(options))
            {
                Check((await context.Entries.FindAsync(paid.EntryId))!.Score == 12.5m, "Unpaid boost does not change score");
            }
            await Reject<InvalidOperationException>(async () =>
                {
                    await using var context = new ApplicationDbContext(options);
                    await new EntryServices(context, clock).DeleteAsync(paid.EntryId.Value);
                }, "Active boost prevents deletion of its entry");
            gateway.Pay(boostSession.Id);
            await Reject<InvalidOperationException>(() => With(service => service.RegisterEntryAsync(boostSession.Id, registration)), "A boost payment cannot be used to register another entry");
            await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => With(service => service.ConfirmAsync(boostSession.Id))));
            await using (var context = new ApplicationDbContext(options))
            {
                Check((await context.Entries.FindAsync(paid.EntryId))!.Score == 22.5m, "Concurrent boost confirmations credit once");
            }

            var independent = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => With(service => service.StartBoostAsync(boost with
                            {
                                ReferenceId = Guid.NewGuid()
                            }))));
            foreach (var checkout in independent)
            {
                gateway.Pay(checkout.Id);
            }
            await Task.WhenAll(independent.Select(checkout => With(service => service.ConfirmAsync(checkout.Id))));
            await using (var context = new ApplicationDbContext(options))
            {
                Check((await context.Entries.FindAsync(paid.EntryId))!.Score == 102.5m, "Independent paid boosts do not overwrite one another");
                Check(await context.ScoreAdditions.Where(row => row.EntryId == paid.EntryId).SumAsync(row => row.Amount) == 102.5m,
                    "Paid score history equals total score");
            }
            var mismatch = await With(service => service.StartBoostAsync(boost with
                    {
                        ReferenceId = Guid.NewGuid()
                    }));
            gateway.Pay(mismatch.Id);
            gateway.ChangeAmount(mismatch.Id);
            await Reject<InvalidOperationException>(() => With(service => service.ConfirmAsync(mismatch.Id)), "Payment amount mismatch is rejected");
            var expired = await With(service => service.StartEntryAsync(Guid.NewGuid(), new CheckoutRequest("Expires creator", 10m)));
            gateway.Expire(expired.Id);
            Check((await With(service => service.ConfirmAsync(expired.Id)))!.Status == "expired", "Stripe expiry is recorded");
            await Reject<InvalidOperationException>(() => With(service => service.RegisterEntryAsync(expired.Id, registration)), "Expired checkout cannot register an entry");
            var recoveryBoost = await With(service => service.StartBoostAsync(boost with
                    {
                        ReferenceId = Guid.NewGuid()
                    }));
            gateway.Pay(recoveryBoost.Id);
            await using (var context = new ApplicationDbContext(options))
            {
                await context.PaymentOperations.Where(operation => operation.Id == recoveryBoost.Id)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(operation => operation.SessionId, (string?)null)
                        .SetProperty(operation => operation.ExpiresAt, clock.GetUtcNow().UtcDateTime.AddMinutes(-1)));
            }
            Check((await With(service => service.ResumeAsync(recoveryBoost.Id)))!.Status == "paid", "Lost session attachment is recovered without a new charge");

            var failedRequest = boost with
            {
                ReferenceId = Guid.NewGuid()
            };
            var failedCheckout = await With(service => service.StartBoostAsync(failedRequest));
            gateway.Fail(failedCheckout.Id);
            Check((await With(service => service.ConfirmAsync(failedCheckout.Id)))!.Status == "failed", "Verified payment failure does not award a score");

            var recoveryEntryId = Guid.NewGuid();
            await using (var context = new ApplicationDbContext(options))
            {
                context.Entries.Add(new Entry(recoveryEntryId)
                    {
                        Name = "Recovery",
                        Username = "recovery",
                        Score = 10m
                    });
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
                context.Entries.Add(new Entry(recoveryEntryId)
                    {
                        Name = "Recovery",
                        Username = "recovery",
                        Score = 10m
                    });
                await context.SaveChangesAsync();
            }
            Check((await With(service => service.ConfirmAsync(recoverable.Id)))!.Fulfilled, "Retry recovers failed fulfillment without charging again");

            var stripe = new StripePaymentGateway(new StripeClient("sk_test_example"), new StripeSettings
                {
                    WebhookSecret = "whsec_example"
                });
            await Reject<InvalidWebhookException>(() => Task.Run(() => stripe.VerifyWebhook("{}", "t=1,v1=invalid")), "Forged webhook signature is rejected");
            var configured = new PaymentSettings
            {
                MinimumAmount = 25m,
                MaximumAmount = 50m
            };
            configured.Validate();
            await Reject<ArgumentException>(() => Task.Run(() => configured.ValidateAmount(10m)), "Changed configuration controls backend limits");
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }
    }

    internal sealed class FakeGateway(TimeProvider clock) : IPaymentGateway
    {
        private readonly ConcurrentDictionary<Guid, VerifiedCheckout> sessions = new();
        public int Created;

        public Task<VerifiedCheckout> CreateAsync(CheckoutRequest request, CheckoutContext context, CancellationToken ct)
        {
            var session = sessions.GetOrAdd(context.OperationId, id =>
                {
                    Interlocked.Increment(ref Created);
                    return new VerifiedCheckout($"cs_{id:N}", id, (long)(request.Amount * 100), context.Currency,
                        "open", false, null, null, "https://checkout.stripe.com/test", false);
                });
            return Task.FromResult(session);
        }

        public Task<VerifiedCheckout?> FindAsync(CheckoutContext context, CancellationToken ct)
        {
            return Task.FromResult(sessions.GetValueOrDefault(context.OperationId));
        }

        public Task<VerifiedCheckout> RetrieveAsync(string sessionId, CancellationToken ct)
        {
            return Task.FromResult(sessions.Values.Single(session => session.SessionId == sessionId));
        }

        public VerifiedCheckout? VerifyWebhook(string payload, string signature)
        {
            if (!Guid.TryParse(payload, out var paymentId))
            {
                throw new InvalidWebhookException();
            }
            return sessions[paymentId];
        }

        public void Pay(Guid id)
        {
            sessions[id] = sessions[id] with
            {
                Paid = true,
                Status = "complete",
                PaymentIntentId = $"pi_{id:N}",
                PaidAt = clock.GetUtcNow().UtcDateTime,
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

        public void Fail(Guid id)
        {
            sessions[id] = sessions[id] with
            {
                Status = "complete",
                Failed = true
            };
        }

        public void ChangeAmount(Guid id)
        {
            sessions[id] = sessions[id] with
            {
                AmountMinor = 1
            };
        }
    }
}
