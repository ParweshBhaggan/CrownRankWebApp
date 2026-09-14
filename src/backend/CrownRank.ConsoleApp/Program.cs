using CrownRank.Application.Abstractions;
using CrownRank.Application.Services;
using CrownRank.ConsoleApp;
using CrownRank.Infrastructure.Configuration;
using CrownRank.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

if (args.Contains("--help"))
{
    Console.WriteLine("CrownRank mock console. Usage: dotnet run --project src/backend/CrownRank.ConsoleApp -- [--database PATH]");
    Console.WriteLine("Admin password for this local-only console: console-admin");
    return;
}
var index = Array.IndexOf(args, "--database");
if (index >= 0 && index + 1 >= args.Length) throw new ArgumentException("--database requires a path.");
var database = index >= 0 ? args[index + 1] : Path.Combine(Environment.CurrentDirectory, "crownrank-console.db");
var services = new ServiceCollection();
services.AddCrownRankSqlite($"Data Source={database}", Path.Combine(Path.GetDirectoryName(Path.GetFullPath(database))!, "assets"),
    developmentMockPayments: true);
services.AddSingleton<ILegalDocumentVersions>(new ConfiguredLegalVersions(new LegalVersions("console-terms-v1", "console-privacy-v1", "console-rules-v1")));
services.AddSingleton<IAdminAuthorization, ConsoleAdminAuthorization>();
services.AddScoped<PaymentService>();
services.AddScoped<EntrySubmissionService>();
services.AddScoped<PublicReadService>();
services.AddScoped<AdminEntryService>();
services.AddScoped<AdminCategoryService>();
services.AddScoped<AdminInspectionService>();
using var provider = services.BuildServiceProvider();
await provider.MigrateCrownRankAsync();
await new ConsoleRunner(provider, Console.In, Console.Out).RunAsync();
