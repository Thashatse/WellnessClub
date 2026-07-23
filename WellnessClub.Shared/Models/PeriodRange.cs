namespace WellnessClub.Shared.Models;

public record PeriodRange(DateOnly Start, DateOnly End)
{
    public long StartEpoch => new DateTimeOffset(Start.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeSeconds();
    public long EndEpoch => new DateTimeOffset(End.ToDateTime(TimeOnly.MinValue).AddDays(1).AddSeconds(-1), TimeSpan.Zero).ToUnixTimeSeconds();
}
