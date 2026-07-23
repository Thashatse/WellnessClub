using System.Text.Json;
using Azure.Data.Tables;
using WellnessClub.Shared.StravaApi;
using WellnessClub.Shared.Sync;
using WellnessClub.Sync;

var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

var configPath = Path.Combine(AppContext.BaseDirectory, "sync-config.json");
if (!File.Exists(configPath))
{
    Console.Error.WriteLine($"sync-config.json not found at {configPath}");
    return 1;
}

var config = JsonSerializer.Deserialize<SyncConfig>(File.ReadAllText(configPath), jsonOpts)!;
var tableServiceClient = new TableServiceClient(config.StorageConnection);
var clubConfigStore = new ClubConfigStore(tableServiceClient);

if (args.Length > 0 && args[0] == "seed-club-config")
{
    var seedPath = Path.Combine(AppContext.BaseDirectory, "club-config.json");
    if (!File.Exists(seedPath))
    {
        Console.Error.WriteLine($"club-config.json not found at {seedPath}");
        return 1;
    }

    var seedConfig = JsonSerializer.Deserialize<ClubConfig>(File.ReadAllText(seedPath), jsonOpts)!;
    await clubConfigStore.UpsertAsync(seedConfig);
    Console.WriteLine($"Club config seeded: anchor {seedConfig.CycleAnchor:yyyy-MM-dd}, {seedConfig.CycleLengthDays}-day cycles.");
    return 0;
}

var http = new HttpClient();
var authClient = new StravaAuthClient(http, config.StravaClientId, config.StravaClientSecret);
var activityClient = new StravaActivityClient(http);
using var rateLimiter = new StravaRateLimiter();

var athleteTable = tableServiceClient.GetTableClient("Athletes");
var clubConfig = await clubConfigStore.GetAsync();

var today = DateOnly.FromDateTime(DateTime.UtcNow);
var current = PeriodCalculator.GetPeriod(clubConfig.CycleAnchor, clubConfig.CycleLengthDays, today);
var previous = PeriodCalculator.GetPreviousPeriod(clubConfig.CycleAnchor, clubConfig.CycleLengthDays, current);

var athletes = athleteTable.Query<TableEntity>().ToList();

if (athletes.Count == 0)
{
    Console.WriteLine("No connected athletes found in Table Storage.");
    return 0;
}

Console.WriteLine($"Syncing period {current.Start:yyyy-MM-dd} → {current.End:yyyy-MM-dd}");
Console.WriteLine($"Syncing {athletes.Count} athlete(s)...");

var engine = new SyncEngine(authClient, activityClient, rateLimiter, athleteTable);
var results = await engine.RunAsync(
    athletes, current, previous, clubConfig.Points, clubConfig.MaxConcurrency,
    onProgress: msg => Console.WriteLine($"  {msg}"));

new ReportGenerator().PrintAndSave(results, config.CompanyName, current.Start.ToString("yyyy-MM-dd"), current.End.ToString("yyyy-MM-dd"));

return 0;
