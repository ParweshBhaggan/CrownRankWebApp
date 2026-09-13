using CrownRank.Application.Abstractions;
using CrownRank.Application.Contracts;
using CrownRank.Application.Payments;
using CrownRank.Domain.Models;
using CrownRank.Domain.ValueObjects;

namespace CrownRank.Application.Services;

public sealed class EntrySubmissionService(ICategoryRepository categories, IEntryRepository entries,
    IProfileImageStorage images, ILegalDocumentVersions legal, IUnitOfWork unitOfWork,
    PaymentService payments, IClock clock)
{
    public async Task<PaymentStart> SubmitAsync(SubmitEntryRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.AcceptedAgreements) throw new InvalidOperationException("Agreement acceptance is required.");
        var category = await categories.GetAsync(request.CategoryId, ct) ?? throw new KeyNotFoundException("Category not found.");
        var versions = await legal.CurrentAsync(ct);
        var acceptance = AgreementAcceptance.Create(versions.Terms, versions.Privacy, versions.Rules, clock.UtcNow);
        var links = request.Links.Select(x => SocialMediaLink.Create(x.Platform, x.Url, x.CustomPlatformName)).ToArray();
        var amount = Money.Create(request.AmountInMinorUnits, request.Currency);
        if (amount.AmountInMinorUnits <= 0) throw new ArgumentException("Payment amount must be positive.");
        var key = await images.SaveAsync(request.Image, request.ImageFileName, ct);
        Entry entry;
        try { entry = Entry.Create(request.Name, request.Username, category, key, acceptance, links, clock.UtcNow); }
        catch
        {
            await images.DeleteAsync(key, ct);
            throw;
        }
        await entries.AddAsync(entry, ct);
        await unitOfWork.SaveAsync(ct);
        return await payments.StartAsync(entry.Id, amount, PaymentPurpose.InitialEntry, ct);
    }
}
