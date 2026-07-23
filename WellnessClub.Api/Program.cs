using Azure.Data.Tables;
using Microsoft.AspNetCore.Authentication.Cookies;
using WellnessClub.Api.Routes;
using WellnessClub.Api.Services;
using WellnessClub.Shared.StravaApi;
using WellnessClub.Shared.Sync;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();

builder.Services.AddSingleton(_ =>
{
    var conn = builder.Configuration["Azure:StorageConnection"]!;
    var client = new TableClient(conn, "Athletes");
    client.CreateIfNotExists();
    return client;
});

builder.Services.AddSingleton(_ => new ClubConfigStore(builder.Configuration["Azure:StorageConnection"]!));

builder.Services.AddSingleton(sp =>
{
    var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
    var config = sp.GetRequiredService<IConfiguration>();
    return new StravaAuthClient(http, config["Strava:ClientId"]!, config["Strava:ClientSecret"]!);
});

builder.Services.AddSingleton(sp =>
    new StravaActivityClient(sp.GetRequiredService<IHttpClientFactory>().CreateClient()));

builder.Services.AddSingleton(_ => new StravaRateLimiter());

builder.Services.AddSingleton(sp => new SyncEngine(
    sp.GetRequiredService<StravaAuthClient>(),
    sp.GetRequiredService<StravaActivityClient>(),
    sp.GetRequiredService<StravaRateLimiter>(),
    sp.GetRequiredService<TableClient>()));

builder.Services.AddScoped<AthleteStore>();
builder.Services.AddScoped<StravaOAuthService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/dashboard/login";
        options.Cookie.Name = "WellnessClubDashboard";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Dashboard", policy => policy.RequireAuthenticatedUser());

var app = builder.Build();

app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapAuthRoutes();
app.MapDashboardRoutes();

app.Run();
