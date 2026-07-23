using System.Net;
using WellnessClub.Api.Services;

namespace WellnessClub.Api.Routes;

public static class AuthRoutes
{
    public static void MapAuthRoutes(this WebApplication app)
    {
        app.MapGet("/auth/strava", (IConfiguration config) =>
            Results.Content(ConsentPage(CompanyName(config)), "text/html"));

        app.MapGet("/auth/strava/go", (StravaOAuthService oauth) =>
            Results.Redirect(oauth.BuildAuthoriseUrl()));

        app.MapGet("/auth/strava/callback", async (string? code, string? error, StravaOAuthService oauth, IConfiguration config) =>
        {
            var companyName = CompanyName(config);

            if (error is not null || code is null)
                return Results.Content(ErrorPage(companyName, "Strava authorisation was denied or cancelled."), "text/html");

            var (athlete, exchangeError) = await oauth.ExchangeCodeAsync(code);

            if (exchangeError is not null)
                return Results.Content(ErrorPage(companyName, exchangeError), "text/html");

            return Results.Content(SuccessPage(companyName, athlete!.DisplayName), "text/html");
        });
    }

    private static string CompanyName(IConfiguration config) => config["CompanyName"]!;

    private static string ConsentPage(string companyName) => TemplateRenderer.RenderPage(
        $"{companyName} Wellness Club", "Consent.html",
        ("CompanyName", WebUtility.HtmlEncode(companyName)));

    private static string SuccessPage(string companyName, string name) => TemplateRenderer.RenderPage(
        $"{companyName} Wellness Club", "Success.html",
        ("CompanyName", WebUtility.HtmlEncode(companyName)),
        ("Name", WebUtility.HtmlEncode(name)));

    private static string ErrorPage(string companyName, string message) => TemplateRenderer.RenderPage(
        $"{companyName} Wellness Club", "Error.html",
        ("Message", WebUtility.HtmlEncode(message)));
}
