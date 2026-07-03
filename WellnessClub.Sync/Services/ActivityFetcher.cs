using System.Net.Http.Headers;
using System.Text.Json;
using WellnessClub.Shared;

namespace WellnessClub.Sync.Services;

public class ActivityFetcher(HttpClient http)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task<List<StravaActivity>> FetchForAthleteAsync(string accessToken, long after, long before)
    {
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var activities = new List<StravaActivity>();
        var page = 1;

        while (true)
        {
            var url = $"https://www.strava.com/api/v3/athlete/activities" +
                      $"?after={after}&before={before}&per_page=100&page={page}";

            var response = await http.GetAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                throw new Exception("Access revoked — athlete needs to re-authorise.");

            response.EnsureSuccessStatusCode();

            var batch = JsonSerializer.Deserialize<List<StravaActivity>>(
                await response.Content.ReadAsStringAsync(), JsonOpts) ?? [];

            if (batch.Count == 0) break;
            activities.AddRange(batch);
            if (batch.Count < 100) break;
            page++;
        }

        return activities;
    }

    public async Task<List<StravaActivity>> FetchClubActivitiesAsync(string accessToken, long after, long before)
    {
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var url = $"https://www.strava.com/api/v3/clubs/1374944/activities?per_page=200";
        var response = await http.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var activities = JsonSerializer.Deserialize<List<StravaActivity>>(
            await response.Content.ReadAsStringAsync(), JsonOpts) ?? [];

        return activities
            .Where(a => a.StartDate.ToUnixTimeSeconds() >= after && a.StartDate.ToUnixTimeSeconds() <= before)
            .ToList();
    }
}
