using Azure.Data.Tables;
using WellnessClub.Shared.Models;

namespace WellnessClub.Api.Services;

public class AthleteStore(TableClient tableClient, IConfiguration config)
{
    // All athletes are grouped under one partition named for the company running the club.
    private string PartitionKey => config["CompanyName"]!.ToLowerInvariant();

    public async Task UpsertAsync(AthleteEntity athlete)
    {
        athlete.PartitionKey = PartitionKey;
        await tableClient.UpsertEntityAsync(athlete);
    }

    public async Task<AthleteEntity?> GetAsync(string stravaAthleteId)
    {
        try
        {
            var response = await tableClient.GetEntityAsync<AthleteEntity>(PartitionKey, stravaAthleteId);
            return response.Value;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }
}
