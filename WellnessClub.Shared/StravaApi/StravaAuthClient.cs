using System.Text.Json;
using WellnessClub.Shared.Models;

namespace WellnessClub.Shared.StravaApi;

public class StravaAuthClient(HttpClient http, string clientId, string clientSecret)
{
    public async Task<(StravaTokenResult Token, StravaAthleteProfile Athlete)?> ExchangeAuthorizationCodeAsync(string code)
    {
        var response = await http.PostAsync("https://www.strava.com/oauth/token", new FormUrlEncodedContent(
        [
            new("client_id", clientId),
            new("client_secret", clientSecret),
            new("code", code),
            new("grant_type", "authorization_code")
        ]));

        if (!response.IsSuccessStatusCode) return null;

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        var token = ParseToken(json);

        var athleteJson = json.GetProperty("athlete");
        var athlete = new StravaAthleteProfile(
            athleteJson.GetProperty("id").GetInt64().ToString(),
            $"{athleteJson.GetProperty("firstname").GetString()} {athleteJson.GetProperty("lastname").GetString()}");

        return (token, athlete);
    }

    public async Task<StravaTokenResult?> RefreshAccessTokenAsync(string refreshToken)
    {
        var response = await http.PostAsync("https://www.strava.com/oauth/token", new FormUrlEncodedContent(
        [
            new("client_id", clientId),
            new("client_secret", clientSecret),
            new("grant_type", "refresh_token"),
            new("refresh_token", refreshToken)
        ]));

        if (!response.IsSuccessStatusCode) return null;

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        return ParseToken(json);
    }

    private static StravaTokenResult ParseToken(JsonElement json) => new(
        json.GetProperty("access_token").GetString()!,
        json.GetProperty("refresh_token").GetString()!,
        DateTimeOffset.FromUnixTimeSeconds(json.GetProperty("expires_at").GetInt64()));
}
