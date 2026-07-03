using WellnessClub.Api.Services;

namespace WellnessClub.Api.Routes;

public static class AuthRoutes
{
    public static void MapAuthRoutes(this WebApplication app)
    {
        app.MapGet("/auth/strava", () => Results.Content(ConsentPage(), "text/html"));

        app.MapGet("/auth/strava/go", (StravaOAuthService oauth) =>
            Results.Redirect(oauth.BuildAuthoriseUrl()));

        app.MapGet("/auth/strava/callback", async (string? code, string? error, StravaOAuthService oauth) =>
        {
            if (error is not null || code is null)
                return Results.Content(ErrorPage("Strava authorisation was denied or cancelled."), "text/html");

            var (athlete, exchangeError) = await oauth.ExchangeCodeAsync(code);

            if (exchangeError is not null)
                return Results.Content(ErrorPage(exchangeError), "text/html");

            return Results.Content(SuccessPage(athlete!.DisplayName), "text/html");
        });
    }

    private static string ConsentPage() => """
        <!DOCTYPE html>
        <html lang="en">
        <head><meta charset="utf-8"><title>Paymenow Wellness Club</title>
        <style>
          * { box-sizing: border-box; }
          body { font-family: -apple-system, sans-serif; display: flex; align-items: center; justify-content: center; min-height: 100vh; margin: 0; background: #f0f4f8; }
          .card { background: white; padding: 2.5rem; border-radius: 16px; box-shadow: 0 4px 24px rgba(0,0,0,.08); max-width: 480px; width: 90%; }
          .logo { font-size: 2.5rem; margin-bottom: 1rem; }
          h1 { color: #1a1a1a; font-size: 1.5rem; margin: 0 0 .5rem; }
          .subtitle { color: #888; font-size: .9rem; margin: 0 0 1.5rem; }
          h2 { font-size: 1rem; color: #333; margin: 1.25rem 0 .5rem; }
          ul { color: #555; font-size: .9rem; padding-left: 1.25rem; margin: 0 0 1rem; line-height: 1.7; }
          .notice { background: #f8f9fa; border-left: 3px solid #FC4C02; padding: .75rem 1rem; border-radius: 0 8px 8px 0; font-size: .85rem; color: #555; margin: 1.25rem 0; }
          .btn { display: block; width: 100%; padding: .9rem; background: #FC4C02; color: white; text-decoration: none; text-align: center; border-radius: 8px; font-size: 1rem; font-weight: 600; margin-top: 1.5rem; }
          .btn:hover { background: #e03e00; }
        </style></head>
        <body>
          <div class="card">
            <div class="logo">🏃</div>
            <h1>Paymenow Wellness Club</h1>
            <p class="subtitle">Connecting your Strava account lets us track your activities automatically for the bi-weekly leaderboard.</p>

            <h2>What we will read from Strava</h2>
            <ul>
              <li>Activity name, date, and sport type</li>
              <li>Duration and distance</li>
              <li>Whether the activity was a personal record (PR)</li>
              <li>Whether others participated (group activity flag)</li>
            </ul>

            <h2>What we will store</h2>
            <ul>
              <li>Your Strava athlete ID and display name</li>
              <li>An access token and refresh token to read your future activities</li>
            </ul>

            <div class="notice">
              We will <strong>never</strong> post to your Strava, read your private activities, or share your data outside Paymenow.
            </div>

            <a class="btn" href="/auth/strava/go">Connect with Strava</a>
          </div>
        </body>
        </html>
        """;

    private static string SuccessPage(string name) => $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head><meta charset="utf-8"><title>Paymenow Wellness Club</title>
        <style>
          body { font-family: -apple-system, sans-serif; display: flex; align-items: center; justify-content: center; height: 100vh; margin: 0; background: #f0f4f8; }
          .card { background: white; padding: 2.5rem; border-radius: 16px; box-shadow: 0 4px 24px rgba(0,0,0,.08); text-align: center; max-width: 420px; width: 90%; }
          .icon { font-size: 3rem; }
          h1 { color: #1a1a1a; font-size: 1.4rem; margin: 1rem 0 .5rem; }
          p { color: #555; font-size: .95rem; line-height: 1.6; }
        </style></head>
        <body>
          <div class="card">
            <div class="icon">✅</div>
            <h1>You're in, {{name}}!</h1>
            <p>Your Strava account is linked to the Paymenow Wellness Club. Your activities will be tracked automatically each competition period.</p>
            <p>You can close this tab.</p>
          </div>
        </body>
        </html>
        """;

    private static string ErrorPage(string message) => $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head><meta charset="utf-8"><title>Paymenow Wellness Club</title>
        <style>
          body { font-family: -apple-system, sans-serif; display: flex; align-items: center; justify-content: center; height: 100vh; margin: 0; background: #f0f4f8; }
          .card { background: white; padding: 2.5rem; border-radius: 16px; box-shadow: 0 4px 24px rgba(0,0,0,.08); text-align: center; max-width: 420px; width: 90%; }
          .icon { font-size: 3rem; }
          h1 { color: #1a1a1a; font-size: 1.4rem; margin: 1rem 0 .5rem; }
          p { color: #555; font-size: .95rem; }
        </style></head>
        <body>
          <div class="card">
            <div class="icon">❌</div>
            <h1>Connection failed</h1>
            <p>{{message}}</p>
          </div>
        </body>
        </html>
        """;
}
