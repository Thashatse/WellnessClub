using Azure;
using Azure.Data.Tables;

namespace WellnessClub.Shared.Models;

internal class ClubConfigEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "config";
    public string RowKey { get; set; } = "club";
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string CycleAnchor { get; set; } = default!;
    public int CycleLengthDays { get; set; } = 14;
    public string ClubId { get; set; } = "";
    public int MaxConcurrency { get; set; } = 8;
    public int PrBonus { get; set; } = 1;
    public int PeriodTotalBonus { get; set; } = 2;
    public int GroupBonus { get; set; } = 1;
    public int RacePoints { get; set; } = 7;
}
