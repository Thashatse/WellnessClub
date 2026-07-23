using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using WellnessClub.Shared.Models;

namespace WellnessClub.Shared.StravaApi;

public class StravaActivityClient(HttpClient http)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task<List<StravaActivity>> FetchActivitiesAsync(string accessToken, long after, long before)
    {
        var activities = new List<StravaActivity>();
        var page = 1;

        while (true)
        {
            var url = $"https://www.strava.com/api/v3/athlete/activities" +
                      $"?after={after}&before={before}&per_page=100&page={page}";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var response = await http.SendAsync(request);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
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

    public async Task<bool> IsClubMemberAsync(string accessToken, string clubId)
    {
        const int perPage = 200;

        for (var page = 1; ; page++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"https://www.strava.com/api/v3/athlete/clubs?page={page}&per_page={perPage}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var response = await http.SendAsync(request);

            if (!response.IsSuccessStatusCode)
                throw new Exception("Could not verify club membership.");

            var clubs = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
            var pageClubs = clubs.EnumerateArray().ToList();

            if (pageClubs.Any(c => c.GetProperty("id").GetInt64().ToString() == clubId))
                return true;

            if (pageClubs.Count < perPage)
                return false;
        }
    }
}
