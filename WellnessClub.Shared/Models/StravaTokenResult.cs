namespace WellnessClub.Shared.Models;

public record StravaTokenResult(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt);
