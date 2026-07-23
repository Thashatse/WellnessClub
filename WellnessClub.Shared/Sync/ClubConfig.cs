namespace WellnessClub.Shared.Sync;

public class PointsConfig
{
    public int PrBonus { get; set; } = 1;           // once per period, if any qualifying activity had a Strava PR
    public int PeriodTotalBonus { get; set; } = 2;  // beat your total distance/time from last period
    public int GroupBonus { get; set; } = 1;
    public int RacePoints { get; set; } = 7;
}

// Non-secret settings shared by the console sync tool and the dashboard, so the two can never drift
// apart on scoring rules or period boundaries. Secrets (Strava credentials, storage connection string)
// stay per-environment and are NOT part of this.
public class ClubConfig
{
    public DateOnly CycleAnchor { get; set; }
    public int CycleLengthDays { get; set; } = 14;
    public string ClubId { get; set; } = "";
    public int MaxConcurrency { get; set; } = 8;
    public PointsConfig Points { get; set; } = new();
}
