namespace WellnessClub.Shared.Models;

public record AthleteResult(string DisplayName, int TotalPoints, List<ScoredActivity> Activities, List<PeriodBonus> PeriodBonuses);
