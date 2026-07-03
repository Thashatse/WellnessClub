using System.Text.Json;
using Azure.Data.Tables;

namespace WellnessClub.Sync.Services;

public class TokenRefresher(HttpClient http, string clientId, string clientSecret)
{
    public async Task<string?> GetValidTokenAsync(TableEntity entity, TableClient tableClient)
    {
        var accessToken = entity.GetString("AccessToken");
        var refreshToken = entity.GetString("RefreshToken");
        var expiry = entity.GetDateTimeOffset("TokenExpiry");

        if (expiry > DateTimeOffset.UtcNow.AddMinutes(5))
            return accessToken;

        var response = await http.PostAsync("https://www.strava.com/oauth/token", new FormUrlEncodedContent(
        [
            new("client_id", clientId),
            new("client_secret", clientSecret),
            new("grant_type", "refresh_token"),
            new("refresh_token", refreshToken!)
        ]));

        if (!response.IsSuccessStatusCode) return null;

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        entity["AccessToken"] = json.GetProperty("access_token").GetString()!;
        entity["RefreshToken"] = json.GetProperty("refresh_token").GetString()!;
        entity["TokenExpiry"] = DateTimeOffset.FromUnixTimeSeconds(json.GetProperty("expires_at").GetInt64());

        await tableClient.UpsertEntityAsync(entity);

        return entity.GetString("AccessToken");
    }
}
