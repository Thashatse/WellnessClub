using System.Text.Json;
using Azure.Data.Tables;
using WellnessClub.Shared;
using WellnessClub.Sync;
using WellnessClub.Sync.Services;

var configPath = Path.Combine(AppContext.BaseDirectory, "sync-config.json");
if (!File.Exists(configPath))
{
    Console.Error.WriteLine($"sync-config.json not found at {configPath}");
    return 1;
}

var config = JsonSerializer.Deserialize<SyncConfig>(File.ReadAllText(configPath),
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

var http = new HttpClient();
var fetcher = new ActivityFetcher(http);
var calculator = new PointsCalculator(config.Points);
var reporter = new ReportGenerator();
var tokenRefresher = new TokenRefresher(http, config.StravaClientId, config.StravaClientSecret);

var tableClient = new TableClient(config.StorageConnection, "Athletes");
var athletes = tableClient.Query<TableEntity>().ToList();

if (athletes.Count == 0)
{
    Console.WriteLine("No connected athletes found in Table Storage.");
    return 0;
}

Console.WriteLine($"Syncing {athletes.Count} athlete(s) for period {config.Period.Start} → {config.Period.End}");

var results = new List<AthleteResult>();

foreach (var entity in athletes)
{
    var athleteId = entity.RowKey;
    var displayName = entity.GetString("DisplayName") ?? athleteId;

    Console.Write($"  Fetching {displayName}... ");

    string? accessToken;
    try
    {
        accessToken = await tokenRefresher.GetValidTokenAsync(entity, tableClient);
        if (accessToken is null)
        {
            Console.WriteLine("⚠ token refresh failed — athlete may need to re-authorise.");
            continue;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠ {ex.Message}");
        continue;
    }

    List<StravaActivity> activities;
    try
    {
        activities = config.UseFallback
            ? await fetcher.FetchClubActivitiesAsync(accessToken, config.Period.StartEpoch, config.Period.EndEpoch)
            : await fetcher.FetchForAthleteAsync(accessToken, config.Period.StartEpoch, config.Period.EndEpoch);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠ {ex.Message}");
        continue;
    }

    activities = Deduplicate(activities);

    float prevBestDistance = 0;
    int prevBestTime = 0;
    float prevTotalDistance = 0;
    int prevTotalTime = 0;

    if (!config.UseFallback && config.PreviousPeriod is not null)
    {
        try
        {
            var prevActivities = await fetcher.FetchForAthleteAsync(
                accessToken, config.PreviousPeriod.StartEpoch, config.PreviousPeriod.EndEpoch);

            var qualifying = prevActivities
                .Where(a => a.MovingTime >= 20 * 60 || a.Distance >= 5000f)
                .ToList();

            if (qualifying.Count > 0)
            {
                prevBestDistance = qualifying.Max(a => a.Distance);
                prevBestTime = qualifying.Max(a => a.MovingTime);
                prevTotalDistance = qualifying.Sum(a => a.Distance);
                prevTotalTime = qualifying.Sum(a => a.MovingTime);
            }
        }
        catch { /* non-critical */ }
    }

    var scored = activities
        .Select(a => calculator.Score(a, prevBestDistance, prevBestTime))
        .Where(s => s.Points > 0)
        .ToList();

    var (periodBonus, periodBonusReason) = calculator.ScorePeriodTotalBonus(scored, prevTotalDistance, prevTotalTime);
    var total = scored.Sum(s => s.Points) + periodBonus;
    Console.WriteLine($"{scored.Count} activities, {total} pts{(periodBonus > 0 ? " (incl. period bonus)" : "")}");

    results.Add(new AthleteResult(displayName, total, scored, periodBonus, periodBonusReason));

    await Task.Delay(300); // stay within Strava rate limits
}

reporter.PrintAndSave(results, config.Period.Start, config.Period.End);

return 0;

static List<StravaActivity> Deduplicate(List<StravaActivity> activities)
{
    var kept = new List<StravaActivity>();

    foreach (var activity in activities.OrderByDescending(a => a.Distance))
    {
        var isDuplicate = kept.Any(k =>
            k.SportType == activity.SportType &&
            Math.Abs((k.StartDate - activity.StartDate).TotalMinutes) <= 5);

        if (!isDuplicate)
            kept.Add(activity);
    }

    return kept;
}
