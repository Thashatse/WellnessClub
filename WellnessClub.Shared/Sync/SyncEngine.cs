using System.Collections.Concurrent;
using Azure.Data.Tables;
using WellnessClub.Shared.Models;
using WellnessClub.Shared.StravaApi;

namespace WellnessClub.Shared.Sync;

// Shared by the console sync tool and the dashboard: syncs every athlete's activities for a period
// in parallel, bounded by MaxConcurrency and Strava's own rate limits (via StravaRateLimiter).
public class SyncEngine(
    StravaAuthClient authClient,
    StravaActivityClient activityClient,
    StravaRateLimiter rateLimiter,
    TableClient athleteTable)
{
    public async Task<List<AthleteResult>> RunAsync(
        List<TableEntity> athletes,
        PeriodRange current,
        PeriodRange previous,
        PointsConfig pointsConfig,
        int maxConcurrency,
        Action<string>? onProgress = null,
        CancellationToken ct = default)
    {
        var calculator = new PointsCalculator(pointsConfig);
        var results = new ConcurrentBag<AthleteResult>();

        await Parallel.ForEachAsync(athletes, new ParallelOptions
        {
            MaxDegreeOfParallelism = maxConcurrency,
            CancellationToken = ct
        }, async (entity, token) =>
        {
            var result = await SyncAthleteAsync(entity, current, previous, calculator, onProgress, token);
            if (result is not null)
                results.Add(result);
        });

        return results.ToList();
    }

    private async Task<AthleteResult?> SyncAthleteAsync(
        TableEntity entity, PeriodRange current, PeriodRange previous,
        PointsCalculator calculator, Action<string>? onProgress, CancellationToken ct)
    {
        var displayName = entity.GetString("DisplayName") ?? entity.RowKey;

        string? accessToken;
        try
        {
            accessToken = await GetValidTokenAsync(entity, ct);
            if (accessToken is null)
            {
                onProgress?.Invoke($"{displayName}: ⚠ token refresh failed — athlete may need to re-authorise.");
                return null;
            }
        }
        catch (Exception ex)
        {
            onProgress?.Invoke($"{displayName}: ⚠ {ex.Message}");
            return null;
        }

        List<StravaActivity> activities;
        try
        {
            await rateLimiter.AcquireReadAsync(ct);
            activities = await activityClient.FetchActivitiesAsync(accessToken, current.StartEpoch, current.EndEpoch);
        }
        catch (Exception ex)
        {
            onProgress?.Invoke($"{displayName}: ⚠ {ex.Message}");
            return null;
        }

        activities = Deduplicate(activities);

        float prevTotalDistance = 0;
        int prevTotalTime = 0;

        try
        {
            await rateLimiter.AcquireReadAsync(ct);
            var prevActivities = await activityClient.FetchActivitiesAsync(accessToken, previous.StartEpoch, previous.EndEpoch);

            var qualifying = prevActivities
                .Where(a => a.MovingTime >= 20 * 60 || a.Distance >= 5000f)
                .ToList();

            if (qualifying.Count > 0)
            {
                prevTotalDistance = qualifying.Sum(a => a.Distance);
                prevTotalTime = qualifying.Sum(a => a.MovingTime);
            }
        }
        catch { /* non-critical */ }

        var scored = activities
            .Select(calculator.Score)
            .Where(s => s.Points > 0)
            .ToList();

        var periodBonuses = new List<PeriodBonus>();

        var (prBonus, prBonusReason) = calculator.ScorePrBonus(scored);
        if (prBonus > 0) periodBonuses.Add(new PeriodBonus(prBonus, prBonusReason!));

        var (totalBonus, totalBonusReason) = calculator.ScorePeriodTotalBonus(scored, prevTotalDistance, prevTotalTime);
        if (totalBonus > 0) periodBonuses.Add(new PeriodBonus(totalBonus, totalBonusReason!));

        var total = scored.Sum(s => s.Points) + periodBonuses.Sum(b => b.Points);

        onProgress?.Invoke($"{displayName}: {scored.Count} activities, {total} pts{(periodBonuses.Count > 0 ? " (incl. period bonus)" : "")}");

        return new AthleteResult(displayName, total, scored, periodBonuses);
    }

    private async Task<string?> GetValidTokenAsync(TableEntity entity, CancellationToken ct)
    {
        var accessToken = entity.GetString("AccessToken");
        var refreshToken = entity.GetString("RefreshToken");
        var expiry = entity.GetDateTimeOffset("TokenExpiry");

        if (expiry > DateTimeOffset.UtcNow.AddMinutes(5))
            return accessToken;

        await rateLimiter.AcquireOverallAsync(ct);
        var refreshed = await authClient.RefreshAccessTokenAsync(refreshToken!);
        if (refreshed is null) return null;

        entity["AccessToken"] = refreshed.AccessToken;
        entity["RefreshToken"] = refreshed.RefreshToken;
        entity["TokenExpiry"] = refreshed.ExpiresAt;
        await athleteTable.UpsertEntityAsync(entity);

        return refreshed.AccessToken;
    }

    private static List<StravaActivity> Deduplicate(List<StravaActivity> activities)
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
}
