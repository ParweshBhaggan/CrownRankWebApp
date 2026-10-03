using CrownRankApp.Application.Dtos.Entry;
using CrownRankApp.Application.Services.Entry;
using CrownRankApp.Domain.Models;
using CrownRankApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CrownRankApp.Infrastructure.Services.Entry
{
    public class EntryServices(ApplicationDbContext context, TimeProvider clock) : IEntryServices
    {
        private IQueryable<Domain.Models.Entry> EntriesWithProfiles()
        {
            return context.Entries
                .AsNoTracking().AsSplitQuery()
                .Include(entry => entry.Categories)
                .Include(entry => entry.SocialMediaPlatforms).ThenInclude(link => link.Platform);
        }

        public Task<List<Domain.Models.Entry>> GetAllAsync()
        {
            return EntriesWithProfiles()
                .OrderByDescending(entry => entry.Score).ThenBy(entry => entry.UpdatedDate ?? entry.CreatedDate).ThenBy(entry => entry.Id)
                .ToListAsync();
        }

        public Task<Domain.Models.Entry?> GetByIdAsync(Guid id)
        {
            return EntriesWithProfiles()
                .FirstOrDefaultAsync(entry => entry.Id == id);
        }

        public async Task<Domain.Models.Entry> CreateAsync(EntryResponseDto dto)
        {
            var name = dto.Name.Trim();
            var username = dto.Username.Trim();
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(username))
            {
                throw new ArgumentException("Name and username are required.");
            }
            if (dto.Score < 1 || dto.Score > 10000 || decimal.Round(dto.Score, 2) != dto.Score)
            {
                throw new ArgumentException("Score must be between 1 and 10000 with at most two decimal places.");
            }
            if (string.IsNullOrWhiteSpace(dto.ImgUrl))
            {
                throw new ArgumentException("A profile image URL is required.");
            }
            if (dto.Categories == null || dto.Categories.Count == 0)
            {
                throw new ArgumentException("At least one category is required.");
            }
            if (dto.SocialMediaPlatforms == null || dto.SocialMediaPlatforms.Count is < 1 or > 5)
            {
                throw new ArgumentException("Add between one and five social profiles.");
            }
            if (await context.Entries.AnyAsync(entry => entry.Username == username))
            {
                throw new InvalidOperationException("An entry with this username already exists.");
            }

            var categoryNames = dto.Categories.Select(category => category.Name.Trim()).Distinct().ToList();
            // Track existing lookup entities so EF reuses them instead of inserting them again.
            var categories = await context.Categories.Where(category => categoryNames.Contains(category.Name)).ToListAsync();
            if (categories.Count != categoryNames.Count)
            {
                throw new ArgumentException("One or more selected categories are unavailable.");
            }
            var platformNames = dto.SocialMediaPlatforms.Select(link => link.PlatformName.Trim()).ToList();
            if (platformNames.Distinct().Count() != platformNames.Count)
            {
                throw new ArgumentException("Social platforms must be unique.");
            }
            var platforms = await context.SocialMediaDefaults.Where(platform => platformNames.Contains(platform.Name)).ToListAsync();
            if (platforms.Count != platformNames.Count)
            {
                throw new ArgumentException("One or more selected social platforms are unavailable.");
            }

            var entry = new Domain.Models.Entry
            {
                Name = name,
                Username = username,
                ImgUrl = dto.ImgUrl,
                Score = dto.Score,
                Categories = categories,
                CreatedDate = clock.GetUtcNow().UtcDateTime
            };
            foreach (var link in dto.SocialMediaPlatforms)
            {
                if (!Uri.TryCreate(link.Url.Trim(), UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(url.UserInfo))
                {
                    throw new ArgumentException("Social profiles require valid HTTPS URLs.");
                }
                var platform = platforms.Single(platform => platform.Name == link.PlatformName.Trim());
                entry.SocialMediaPlatforms.Add(new SocialMediaPlatform
                    {
                        PlatformId = platform.Id,
                        Platform = platform,
                        Url = link.Url.Trim(),
                        Entry = entry
                    });
            }
            // Save the entry and all links together; the entry FK exists before links are inserted.
            context.Entries.Add(entry);
            context.ScoreAdditions.Add(new ScoreAddition
                {
                    Entry = entry,
                    Amount = entry.Score,
                    CreatedDate = entry.CreatedDate
                });
            await context.SaveChangesAsync();
            return entry;
        }

        public async Task<Domain.Models.Entry?> BoostScoreAsync(Guid id, decimal amount, Guid? referenceId = null)
        {
            if (amount <= 0 || amount > 10000 || decimal.Round(amount, 2) != amount)
            {
                throw new ArgumentException("Boost amount must be positive, at most 10000, and have at most two decimal places.");
            }
            if (referenceId == Guid.Empty)
            {
                throw new ArgumentException("Boost reference cannot be empty.");
            }
            var additionId = referenceId ?? Guid.NewGuid();
            var existing = await context.ScoreAdditions.AsNoTracking().FirstOrDefaultAsync(addition => addition.Id == additionId);
            if (existing != null)
            {
                ValidateRetry(existing, id, amount);
                return await GetByIdAsync(id);
            }

            await using var transaction = await context.Database.BeginTransactionAsync();
            // Increment in SQL, so concurrent boosts cannot overwrite one another.
            const decimal maxScore = 9999999999999999.99m;
            // numeric(18,2)
            var changed = await context.Entries.Where(entry => entry.Id == id && entry.Score <= maxScore - amount)
                .ExecuteUpdateAsync(setters => setters.SetProperty(entry => entry.Score, entry => entry.Score + amount));
            if (changed == 0)
            {
                if (await context.Entries.AnyAsync(entry => entry.Id == id))
                {
                    throw new ArgumentException("This boost would exceed the maximum supported score.");
                }
                return null;
            }
            // Take the timestamp after acquiring the row lock, keeping concurrent tie dates in order.
            var now = clock.GetUtcNow().UtcDateTime;
            await context.Entries.Where(entry => entry.Id == id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(entry => entry.UpdatedDate, now));
            context.ScoreAdditions.Add(new ScoreAddition(additionId)
                {
                    EntryId = id,
                    Amount = amount,
                    CreatedDate = now
                });
            try
            {
                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (DbUpdateException)
            {
                // A concurrent retry may have saved this reference first; undo our increment.
                await transaction.RollbackAsync();
                context.ChangeTracker.Clear();
                existing = await context.ScoreAdditions.AsNoTracking().FirstOrDefaultAsync(addition => addition.Id == additionId);
                if (existing == null)
                {
                    throw;
                }
                ValidateRetry(existing, id, amount);
            }
            return await GetByIdAsync(id);
        }

        private static void ValidateRetry(ScoreAddition addition, Guid id, decimal amount)
        {
            if (addition.EntryId != id || addition.Amount != amount)
            {
                throw new InvalidOperationException("This boost reference was already used with different details.");
            }
        }

        public async Task<List<DailyEntryResponseDto>> GetDailyAsync(DateOnly date)
        {
            var start = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var end = start.AddDays(1);
            var scores = await context.ScoreAdditions.AsNoTracking()
                .Where(addition => addition.CreatedDate >= start && addition.CreatedDate < end)
                .GroupBy(addition => addition.EntryId)
                .Select(group => new
                {
                    EntryId = group.Key,
                    Score = group.Sum(addition => addition.Amount),
                    ReachedDate = group.Max(addition => addition.CreatedDate)
                })
                .OrderByDescending(row => row.Score).ThenBy(row => row.ReachedDate).ThenBy(row => row.EntryId)
                .ToListAsync();
            var ids = scores.Select(row => row.EntryId).ToList();
            var entries = await EntriesWithProfiles().Where(entry => ids.Contains(entry.Id)).ToDictionaryAsync(entry => entry.Id);
            return scores.Where(row => entries.ContainsKey(row.EntryId)).Select(row => new DailyEntryResponseDto
                {
                    Entry = EntryResponseDto.FromEntry(entries[row.EntryId]),
                    DailyScore = row.Score,
                    ScoreReachedDate = row.ReachedDate
                }).ToList();
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            await using var transaction = await context.Database.BeginTransactionAsync();
            var locked = await context.Entries.Where(entry => entry.Id == id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(entry => entry.Score, entry => entry.Score));
            if (locked == 0)
            {
                return false;
            }
            if (await context.PaymentOperations.AnyAsync(payment => payment.EntryId == id
                    && payment.FulfilledAt == null && payment.Status != PaymentStatus.Expired && payment.Status != PaymentStatus.Failed))
            {
                throw new InvalidOperationException("Cannot delete an entry with an active payment.");
            }
            var entry = await context.Entries.FirstAsync(entry => entry.Id == id);
            context.Entries.Remove(entry);
            // Social links are deleted by the configured cascade.
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }
    }
}
