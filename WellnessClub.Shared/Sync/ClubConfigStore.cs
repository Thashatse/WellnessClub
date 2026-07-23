using Azure;
using Azure.Data.Tables;
using WellnessClub.Shared.Models;

namespace WellnessClub.Shared.Sync;

// The console sync tool and the dashboard both point their TableServiceClient at the same storage
// account (they already share it for the Athletes table) and read this one row, so scoring/period
// config is defined exactly once.
public class ClubConfigStore(TableServiceClient serviceClient)
{
    private const string TableName = "ClubConfig";
    private const string PartitionKey = "config";
    private const string RowKey = "club";

    public ClubConfigStore(string storageConnection) : this(new TableServiceClient(storageConnection))
    {
    }

    public async Task<ClubConfig> GetAsync()
    {
        var table = serviceClient.GetTableClient(TableName);
        await table.CreateIfNotExistsAsync();

        try
        {
            var response = await table.GetEntityAsync<ClubConfigEntity>(PartitionKey, RowKey);
            return ToClubConfig(response.Value);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            throw new InvalidOperationException(
                "No ClubConfig row found in Table Storage. Seed one with ClubConfigStore.UpsertAsync before syncing.");
        }
    }

    public async Task UpsertAsync(ClubConfig config)
    {
        var table = serviceClient.GetTableClient(TableName);
        await table.CreateIfNotExistsAsync();
        await table.UpsertEntityAsync(ToEntity(config));
    }

    private static ClubConfig ToClubConfig(ClubConfigEntity entity) => new()
    {
        CycleAnchor = DateOnly.Parse(entity.CycleAnchor),
        CycleLengthDays = entity.CycleLengthDays,
        ClubId = entity.ClubId,
        MaxConcurrency = entity.MaxConcurrency,
        Points = new PointsConfig
        {
            PrBonus = entity.PrBonus,
            PeriodTotalBonus = entity.PeriodTotalBonus,
            GroupBonus = entity.GroupBonus,
            RacePoints = entity.RacePoints
        }
    };

    private static ClubConfigEntity ToEntity(ClubConfig config) => new()
    {
        CycleAnchor = config.CycleAnchor.ToString("yyyy-MM-dd"),
        CycleLengthDays = config.CycleLengthDays,
        ClubId = config.ClubId,
        MaxConcurrency = config.MaxConcurrency,
        PrBonus = config.Points.PrBonus,
        PeriodTotalBonus = config.Points.PeriodTotalBonus,
        GroupBonus = config.Points.GroupBonus,
        RacePoints = config.Points.RacePoints
    };
}
