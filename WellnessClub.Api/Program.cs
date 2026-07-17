using Azure.Data.Tables;
using WellnessClub.Api.Routes;
using WellnessClub.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();

builder.Services.AddSingleton(_ =>
{
    var conn = builder.Configuration["Azure:StorageConnection"]!;
    var client = new TableClient(conn, "Athletes");
    client.CreateIfNotExists();
    return client;
});

builder.Services.AddScoped<AthleteStore>();
builder.Services.AddScoped<StravaOAuthService>();

var app = builder.Build();

app.UseStaticFiles();

app.MapAuthRoutes();

app.Run();
