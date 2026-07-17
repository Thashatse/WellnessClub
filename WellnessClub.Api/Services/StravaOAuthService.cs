using System.Text.Json;
using WellnessClub.Shared;

namespace WellnessClub.Api.Services;

public class StravaOAuthService(IHttpClientFactory httpFactory, IConfiguration config, AthleteStore store)
{
    public string BuildAuthoriseUrl()
    {
        var clientId = config["Strava:ClientId"];
        var redirectUri = config["Strava:RedirectUri"];
        return $"https://www.strava.com/oauth/authorize" +
               $"?client_id={clientId}" +
               $"&response_type=code" +
               $"&redirect_uri={Uri.EscapeDataString(redirectUri!)}" +
               $"&approval_prompt=force" +
               $"&scope=activity:read";
    }

    public async Task<(AthleteEntity? Athlete, string? Error)> ExchangeCodeAsync(string code)
    {
        var client = httpFactory.CreateClient();
        var response = await client.PostAsync("https://www.strava.com/oauth/token", new FormUrlEncodedContent(
        [
            new("client_id", config["Strava:ClientId"]!),
            new("client_secret", config["Strava:ClientSecret"]!),
            new("code", code),
            new("grant_type", "authorization_code")
        ]));

        if (!response.IsSuccessStatusCode)
            return (null, "Failed to exchange code with Strava.");

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        var accessToken = json.GetProperty("access_token").GetString()!;
        var refreshToken = json.GetProperty("refresh_token").GetString()!;
        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(json.GetProperty("expires_at").GetInt64());
        var athleteJson = json.GetProperty("athlete");
        var athleteId = athleteJson.GetProperty("id").GetInt64().ToString();
        var displayName = $"{athleteJson.GetProperty("firstname").GetString()} {athleteJson.GetProperty("lastname").GetString()}";

        // Verify club membership
        var clubCheckClient = httpFactory.CreateClient();
        clubCheckClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var clubId = config["Strava:ClubId"];
        var isMember = false;
        const int perPage = 200;
        for (var page = 1; ; page++)
        {
            var clubsResponse = await clubCheckClient.GetAsync(
                $"https://www.strava.com/api/v3/athlete/clubs?page={page}&per_page={perPage}");
            if (!clubsResponse.IsSuccessStatusCode)
                return (null, "Could not verify club membership.");

            var clubs = JsonDocument.Parse(await clubsResponse.Content.ReadAsStringAsync()).RootElement;
            var pageClubs = clubs.EnumerateArray().ToList();
            if (pageClubs.Any(c => c.GetProperty("id").GetInt64().ToString() == clubId))
            {
                isMember = true;
                break;
            }

            if (pageClubs.Count < perPage)
                break;
        }

        if (!isMember)
            return (null, "You are not a member of the Paymenow Wellness Club on Strava.");

        var athlete = new AthleteEntity
        {
            RowKey = athleteId,
            DisplayName = displayName,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            TokenExpiry = expiresAt
        };

        await store.UpsertAsync(athlete);
        return (athlete, null);
    }
}
