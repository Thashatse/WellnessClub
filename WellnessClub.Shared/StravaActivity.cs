using System.Text.Json.Serialization;

namespace WellnessClub.Shared;

public class StravaActivity
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = default!;

    [JsonPropertyName("type")]
    public string Type { get; set; } = default!;

    [JsonPropertyName("sport_type")]
    public string SportType { get; set; } = default!;

    [JsonPropertyName("distance")]
    public float Distance { get; set; } // metres

    [JsonPropertyName("moving_time")]
    public int MovingTime { get; set; } // seconds

    [JsonPropertyName("start_date")]
    public DateTimeOffset StartDate { get; set; }

    [JsonPropertyName("athlete_count")]
    public int AthleteCount { get; set; }

    [JsonPropertyName("workout_type")]
    public int? WorkoutType { get; set; }

    [JsonPropertyName("pr_count")]
    public int PrCount { get; set; }
}
