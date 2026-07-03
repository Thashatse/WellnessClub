using WellnessClub.Shared;
using WellnessClub.Sync;

namespace WellnessClub.Sync.Services;

public record ScoredActivity(StravaActivity Activity, int Points, List<string> Reasons, bool NeedsReview);

public class PointsCalculator(PointsConfig config)
{
    private const int BaseMinSeconds = 20 * 60;
    private const float BaseMinMetres = 5000f;

    public ScoredActivity Score(StravaActivity activity, float? previousPeriodBestDistance, int? previousPeriodBestTime)
    {
        var reasons = new List<string>();
        var needsReview = false;
        int points;

        var qualifies = activity.MovingTime >= BaseMinSeconds || activity.Distance >= BaseMinMetres;
        if (!qualifies)
            return new ScoredActivity(activity, 0, ["Does not meet 20 min / 5 km minimum"], false);

        points = 1;
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

            if (previousPeriodBestDistance.HasValue && activity.Distance > previousPeriodBestDistance.Value)
            {
                points += config.PeriodBestBonus;
                reasons.Add($"Exceeded previous period distance best (+{config.PeriodBestBonus} pts)");
            }
            else if (previousPeriodBestTime.HasValue && activity.MovingTime > previousPeriodBestTime.Value)
            {
                points += config.PeriodBestBonus;
                reasons.Add($"Exceeded previous period time best (+{config.PeriodBestBonus} pts)");
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
}
