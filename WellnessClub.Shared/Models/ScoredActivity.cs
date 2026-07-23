namespace WellnessClub.Shared.Models;

public record ScoredActivity(StravaActivity Activity, int Points, List<string> Reasons, bool NeedsReview);
