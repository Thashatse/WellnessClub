using WellnessClub.Shared.Models;
using WellnessClub.Shared.StravaApi;

namespace WellnessClub.Api.Services;

public class StravaOAuthService(
    StravaAuthClient authClient, StravaActivityClient activityClient, IConfiguration config, AthleteStore store)
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
        var exchange = await authClient.ExchangeAuthorizationCodeAsync(code);
        if (exchange is null)
            return (null, "Failed to exchange code with Strava.");

        var (token, profile) = exchange.Value;
        var clubId = config["Strava:ClubId"]!;

        bool isMember;
        try
        {
            isMember = await activityClient.IsClubMemberAsync(token.AccessToken, clubId);
        }
        catch
        {
            return (null, "Could not verify club membership.");
        }

        if (!isMember)
            return (null, $"You are not a member of the {config["CompanyName"]} Wellness Club on Strava.");

        var athlete = new AthleteEntity
        {
            RowKey = profile.AthleteId,
            DisplayName = profile.DisplayName,
            AccessToken = token.AccessToken,
            RefreshToken = token.RefreshToken,
            TokenExpiry = token.ExpiresAt
        };

        await store.UpsertAsync(athlete);
        return (athlete, null);
    }
}
