using Azure.Data.Tables;
using WellnessClub.Shared;

namespace WellnessClub.Api.Services;

public class AthleteStore(TableClient tableClient)
{
    public async Task UpsertAsync(AthleteEntity athlete)
    {
        await tableClient.UpsertEntityAsync(athlete);
    }

    public async Task<AthleteEntity?> GetAsync(string stravaAthleteId)
    {
        try
        {
            var response = await tableClient.GetEntityAsync<AthleteEntity>("paymenow", stravaAthleteId);
            return response.Value;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }
}
