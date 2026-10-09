using System.Text.Json;

namespace CrownRankApp.API.Logging;

public sealed class DailyEndpointLogWriter(IWebHostEnvironment environment, TimeProvider clock)
{
    private readonly SemaphoreSlim writeLock = new(1, 1);
    private readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web);

    public async Task WriteAsync(string area, EndpointLogEntry entry, CancellationToken ct = default)
    {
        try
        {
            var localDate = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
            var directory = Path.Combine(environment.ContentRootPath, "Logs", area);
            var path = Path.Combine(directory, $"{localDate:yyyy-MM-dd}.txt");
            var line = JsonSerializer.Serialize(entry, jsonOptions) + Environment.NewLine;

            await writeLock.WaitAsync(ct);
            try
            {
                Directory.CreateDirectory(directory);
                await File.AppendAllTextAsync(path, line, ct);
            }
            finally
            {
                writeLock.Release();
            }
        }
        catch
        {
            // Logging must never break an otherwise valid application request.
        }
    }
}

public sealed record EndpointLogEntry(
    DateTimeOffset Timestamp,
    string Method,
    string Path,
    int StatusCode,
    long DurationMs,
    string TraceId,
    string Source,
    string? Endpoint);
