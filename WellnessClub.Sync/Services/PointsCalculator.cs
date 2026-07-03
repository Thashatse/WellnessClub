using WellnessClub.Shared;
using WellnessClub.Sync;

namespace WellnessClub.Sync.Services;

public record ScoredActivity(StravaActivity Activity, int Points, List<string> Reasons, bool NeedsReview);

public class PointsCalculator(PointsConfig config)
{
    private const int BaseMinSeconds = 20 * 60;
    private const float BaseMinMetres = 5000f;

    public ScoredActivity Score(StravaActivity activity, float prevBestDistance, int prevBestTime)
    {
        var reasons = new List<string>();
        var needsReview = false;

        var qualifies = activity.MovingTime >= BaseMinSeconds || activity.Distance >= BaseMinMetres;
        if (!qualifies)
            return new ScoredActivity(activity, 0, ["Does not meet 20 min / 5 km minimum"], false);

        var points = 1;
        reasons.Add("Base (1 pt)");

        if (activity.WorkoutType == 1 && config.RacePoints > 0)
        {
            points += config.RacePoints;
            reasons.Add($"Race (+{config.RacePoints} pts)");
        }
        else
        {
            if (activity.PrCount > 0)
            {
                points += config.PrBonus;
                reasons.Add($"Strava PR (+{config.PrBonus} pts)");
            }

            if (config.ActivityBestBonus > 0 && prevBestDistance > 0 && activity.Distance > prevBestDistance)
            {
                points += config.ActivityBestBonus;
                reasons.Add($"Beat previous period best distance ({activity.Distance / 1000:F1} km vs {prevBestDistance / 1000:F1} km) (+{config.ActivityBestBonus} pts)");
            }
            else if (config.ActivityBestBonus > 0 && prevBestTime > 0 && activity.MovingTime > prevBestTime)
            {
                var curr = TimeSpan.FromSeconds(activity.MovingTime);
                var prev = TimeSpan.FromSeconds(prevBestTime);
                points += config.ActivityBestBonus;
                reasons.Add($"Beat previous period best time ({(int)curr.TotalHours}h {curr.Minutes:D2}m vs {(int)prev.TotalHours}h {prev.Minutes:D2}m) (+{config.ActivityBestBonus} pts)");
            }

            if (activity.AthleteCount > 1)
            {
                points += config.GroupBonus;
                reasons.Add($"Group activity (+{config.GroupBonus} pt) ⚑ verify PMN colleague");
                needsReview = true;
            }
        }

        return new ScoredActivity(activity, points, reasons, needsReview);
    }

    public (int Bonus, string? Reason) ScorePeriodTotalBonus(
        List<ScoredActivity> current,
        float prevTotalDistance, int prevTotalTime)
    {
        if (config.PeriodTotalBonus == 0) return (0, null);

        var currentDistance = current.Sum(a => a.Activity.Distance);
        var currentTime = current.Sum(a => a.Activity.MovingTime);

        if (prevTotalDistance > 0 && currentDistance > prevTotalDistance)
            return (config.PeriodTotalBonus, $"Beat previous period total distance ({currentDistance / 1000:F1} km vs {prevTotalDistance / 1000:F1} km) (+{config.PeriodTotalBonus} pts)");

        if (prevTotalTime > 0 && currentTime > prevTotalTime)
        {
            var curr = TimeSpan.FromSeconds(currentTime);
            var prev = TimeSpan.FromSeconds(prevTotalTime);
            return (config.PeriodTotalBonus, $"Beat previous period total time ({(int)curr.TotalHours}h {curr.Minutes:D2}m vs {(int)prev.TotalHours}h {prev.Minutes:D2}m) (+{config.PeriodTotalBonus} pts)");
        }

        return (0, null);
    }
}
