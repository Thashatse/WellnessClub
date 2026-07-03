using Azure;
using Azure.Data.Tables;

namespace WellnessClub.Shared;

public class AthleteEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "paymenow";
    public string RowKey { get; set; } = default!; // Strava athlete ID
    public string DisplayName { get; set; } = default!;
    public string AccessToken { get; set; } = default!;
    public string RefreshToken { get; set; } = default!;
    public DateTimeOffset TokenExpiry { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}
