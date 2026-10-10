namespace CrownRankApp.API.Logging;

public sealed class DailyEndpointLogWriter(IWebHostEnvironment environment, TimeProvider clock)
{
    private readonly SemaphoreSlim writeLock = new(1, 1);

    public async Task WriteAsync(string area, EndpointLogEntry entry, CancellationToken ct = default)
    {
        try
        {
            var localDate = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
            var directory = Path.Combine(environment.ContentRootPath, "Logs", area);
            var path = Path.Combine(directory, $"{localDate:dd-MM-yyyy}.txt");
            var block =
                $"Log {entry.Timestamp:dd-MM-yyyy HH:mm:ss}{Environment.NewLine}" +
                $"Text: {entry.Text}{Environment.NewLine}" +
                $"Who: {entry.Who}{Environment.NewLine}" +
                $"Endpoint: {entry.Endpoint}{Environment.NewLine}" +
                $"Response: {entry.Response}{Environment.NewLine}" +
                $"Request: {entry.Request}{Environment.NewLine}" +
                $"{new string('-', 60)}{Environment.NewLine}";

            await writeLock.WaitAsync(ct);
            try
            {
                Directory.CreateDirectory(directory);
                await File.AppendAllTextAsync(path, block, ct);
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
    string Text,
    string Who,
    string Endpoint,
    string Response,
    string Request);
