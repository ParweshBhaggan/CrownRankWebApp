using CrownRankApp.Application.Dtos.Category;
using CrownRankApp.Application.Dtos.Entry;
using CrownRankApp.Application.Dtos.SocialMedia;
using CrownRankApp.Infrastructure.Data;
using CrownRankApp.Infrastructure.Services.Entry;
using Microsoft.EntityFrameworkCore;
using Npgsql;

// Uses a freshly created disposable database; never modifies the supplied database.
var connection = Environment.GetEnvironmentVariable("CROWNRANK_TEST_CONNECTION")
    ?? throw new InvalidOperationException("Set CROWNRANK_TEST_CONNECTION to a PostgreSQL connection with permission to create test databases.");
var builder = new NpgsqlConnectionStringBuilder(connection);
var databaseName = $"crownrank_test_{Guid.NewGuid():N}";
await using var admin = new NpgsqlConnection(builder.ConnectionString);
await admin.OpenAsync();
await using (var command = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin)) await command.ExecuteNonQueryAsync();
builder.Database = databaseName;
var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(builder.ConnectionString).Options;
var clock = new TestClock(new DateTime(2026, 9, 28, 10, 0, 0, DateTimeKind.Utc));
ApplicationDbContext Context() => new(options);
EntryServices Service(ApplicationDbContext context) => new(context, clock);

async Task<Guid> Create(string username, decimal score)
{
    await using var context = Context();
    return (await Service(context).CreateAsync(new EntryResponseDto
    {
        Name = username, Username = username, Score = score, ImgUrl = "https://example.com/profile.webp",
        Categories = [new CategoryDto { Name = "Technology" }],
        SocialMediaPlatforms = [new SocialMediaPlatformDto { PlatformName = "Instagram", Url = $"https://instagram.com/{username}" }]
    })).Id;
}
async Task<decimal> Score(Guid id)
{
    await using var context = Context();
    return (await Service(context).GetByIdAsync(id))!.Score;
}
async Task Boost(Guid id, decimal amount, Guid? reference = null)
{
    await using var context = Context();
    Check(await Service(context).BoostScoreAsync(id, amount, reference) != null, "Boost returned an entry");
}
async Task<List<DailyEntryResponseDto>> Daily(int day)
{
    await using var context = Context();
    return await Service(context).GetDailyAsync(new DateOnly(2026, 9, day));
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
    await using (var context = Context()) await context.Database.MigrateAsync();
    var first = await Create("first", 20m);
    clock.Now = new DateTime(2026, 9, 28, 11, 0, 0, DateTimeKind.Utc);
    var second = await Create("second", 15m);
    await using (var context = Context())
    {
        Check(await context.Categories.CountAsync() == 16 && await context.SocialMediaDefaults.CountAsync() == 8, "Existing lookup rows are reused");
        Check(await context.ScoreAdditions.CountAsync() == 2, "Opening scores are recorded");
    }
    clock.Now = new DateTime(2026, 9, 29, 10, 0, 0, DateTimeKind.Utc);
    await Boost(second, 5m);
    await using (var context = Context())
        Check((await Service(context).GetAllAsync())[0].Id == first, "Equal global scores prefer the earlier score date");
    clock.Now = new DateTime(2026, 9, 29, 11, 0, 0, DateTimeKind.Utc);
    await Boost(second, 5m);
    clock.Now = new DateTime(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);
    await Boost(first, 5m);
    await using (var context = Context())
        Check((await Service(context).GetAllAsync())[0].Id == second, "Tie order uses UpdatedDate rather than original creation date");
    var yesterday = await Daily(28);
    var today = await Daily(29);
    Check(yesterday.Single(row => row.Entry.Id == first).DailyScore == 20m, "Later boosts do not change historical daily scores");
    Check(today[0].Entry.Id == second && today[0].DailyScore == 10m && today[1].DailyScore == 5m, "Daily ranking uses daily additions rather than all-time scores");
    Check(today[0].Entry.Score == 25m, "Daily response retains the entry's global score");
    foreach (var amount in new[] { -1m, 0m, 0.001m, 10000.01m })
        await Reject<ArgumentException>(() => Boost(first, amount), $"Reject boost amount {amount}");
    Check(await Score(first) == 25m, "Invalid boosts leave the score unchanged");
    await using (var context = Context())
        Check(await Service(context).BoostScoreAsync(Guid.NewGuid(), 1m) == null, "Unknown entry returns not found");

    var reference = Guid.NewGuid();
    await Boost(first, 0.25m, reference);
    var reachedAt = clock.Now;
    clock.Now = clock.Now.AddHours(1);
    await Boost(first, 0.25m, reference);
    Check(await Score(first) == 25.25m, "Retrying a boost reference does not credit it twice");
    await using (var context = Context())
        Check((await Service(context).GetByIdAsync(first))!.UpdatedDate == reachedAt, "A retry leaves the tie timestamp unchanged");
    await Reject<InvalidOperationException>(() => Boost(first, 1m, reference), "Reject reference reused with a different amount");
    await Reject<InvalidOperationException>(() => Boost(second, 0.25m, reference), "Reject reference reused for a different entry");

    var concurrent = await Create("concurrent", 1m);
    await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Boost(concurrent, 1.25m)));
    Check(await Score(concurrent) == 16m, "Concurrent independent boosts retain every addition");
    var sharedReference = Guid.NewGuid();
    await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Boost(concurrent, 2m, sharedReference)));
    Check(await Score(concurrent) == 18m, "Concurrent retries credit one boost only");
    await using (var context = Context())
        Check(await context.ScoreAdditions.Where(row => row.EntryId == concurrent).SumAsync(row => row.Amount) == 18m, "Score and history totals remain consistent");

    clock.Now = new DateTime(2026, 9, 29, 23, 59, 59, DateTimeKind.Utc);
    var boundary = await Create("boundary", 3m);
    clock.Now = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc);
    await Boost(boundary, 2m);
    Check((await Daily(29)).Single(row => row.Entry.Id == boundary).DailyScore == 3m, "Daily range includes additions before midnight");
    Check((await Daily(30)).Single(row => row.Entry.Id == boundary).DailyScore == 2m, "Midnight additions belong only to the next UTC day");
    Check((await Daily(27)).Count == 0, "A day without additions returns an empty ranking");

    var tieEarlier = await Create("tie_earlier", 7m);
    clock.Now = clock.Now.AddMinutes(1);
    var tieLater = await Create("tie_later", 7m);
    var tied = (await Daily(30)).Where(row => row.Entry.Id == tieEarlier || row.Entry.Id == tieLater).ToList();
    Check(tied[0].Entry.Id == tieEarlier, "Daily ties prefer the earlier score timestamp");
    await using (var context = Context())
    {
        await Service(context).DeleteAsync(boundary);
        Check(!await context.ScoreAdditions.AnyAsync(row => row.EntryId == boundary), "Deleting an entry cascades its score additions");
    }
    Console.WriteLine("All PostgreSQL integration checks passed.");
}
finally
{
    NpgsqlConnection.ClearAllPools();
    await using var command = new NpgsqlCommand($"DROP DATABASE \"{databaseName}\" WITH (FORCE)", admin);
    await command.ExecuteNonQueryAsync();
}

sealed class TestClock(DateTime now) : TimeProvider
{
    public DateTime Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => new(Now);
}
