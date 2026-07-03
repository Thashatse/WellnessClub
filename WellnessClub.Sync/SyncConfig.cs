namespace WellnessClub.Sync;

public class SyncConfig
{
    public string StorageConnection { get; set; } = default!;
    public string StravaClientId { get; set; } = default!;
    public string StravaClientSecret { get; set; } = default!;
    public bool UseFallback { get; set; } = false;
    public PeriodConfig Period { get; set; } = default!;
    public PeriodConfig? PreviousPeriod { get; set; }
    public PointsConfig Points { get; set; } = new();
}

public class PeriodConfig
{
    public string Start { get; set; } = default!;
    public string End { get; set; } = default!;

    public long StartEpoch => new DateTimeOffset(DateTime.Parse(Start), TimeSpan.Zero).ToUnixTimeSeconds();
    public long EndEpoch => new DateTimeOffset(DateTime.Parse(End).AddDays(1).AddSeconds(-1), TimeSpan.Zero).ToUnixTimeSeconds();
}

public class PointsConfig
{
    public int PrBonus { get; set; } = 2;
    public int PeriodBestBonus { get; set; } = 2;
    public int GroupBonus { get; set; } = 1;
    public int RacePoints { get; set; } = 7;
}
