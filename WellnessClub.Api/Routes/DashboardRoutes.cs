using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Azure.Data.Tables;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using WellnessClub.Api.Services;
using WellnessClub.Shared.Models;
using WellnessClub.Shared.Sync;

namespace WellnessClub.Api.Routes;

public static class DashboardRoutes
{
    public static void MapDashboardRoutes(this WebApplication app)
    {
        app.MapGet("/dashboard/login", (IConfiguration config) => Results.Content(LoginPage(config), "text/html"));

        app.MapPost("/dashboard/login", async (HttpContext ctx, IConfiguration config) =>
        {
            var form = await ctx.Request.ReadFormAsync();
            var password = form["password"].ToString();
            var expected = config["Dashboard:Password"];

            if (string.IsNullOrEmpty(expected) || !PasswordMatches(password, expected))
                return Results.Content(LoginPage(config, error: true), "text/html");

            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, "bernice")], CookieAuthenticationDefaults.AuthenticationScheme);
            await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

            return Results.Redirect("/dashboard");
        });

        var group = app.MapGroup("/dashboard").RequireAuthorization("Dashboard");

        group.MapGet("", (ClubConfigStore configStore, IConfiguration config, int? purged) =>
            ShowDashboardAsync(configStore, config, purged));

        group.MapPost("/reports", (
                HttpRequest request, ClubConfigStore configStore, TableClient athleteTable, SyncEngine engine, IConfiguration config) =>
            GenerateReportAsync(request, configStore, athleteTable, engine, config));

        group.MapGet("/reports/{file}.csv", (string file, IConfiguration config) => DownloadReport(file, config));
        group.MapGet("/reports/{file}.md", (string file, IConfiguration config) => DownloadMarkdown(file, config));

        group.MapPost("/purge", (IConfiguration config) => PurgeAsync(config));

        group.MapGet("/employees", (TableClient athleteTable, IConfiguration config) => ShowEmployees(athleteTable, config));

        group.MapGet("/settings", (ClubConfigStore configStore, IConfiguration config) => ShowSettingsAsync(configStore, config));
        group.MapPost("/settings", (
                HttpRequest request, ClubConfigStore configStore, IConfiguration config) =>
            SaveSettingsAsync(request, configStore, config));
    }

    private static async Task<IResult> ShowDashboardAsync(ClubConfigStore configStore, IConfiguration config, int? purged)
    {
        var clubConfig = await configStore.GetAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var periods = PeriodCalculator.ListPeriods(clubConfig.CycleAnchor, clubConfig.CycleLengthDays, today);

        var reportsDir = ReportsDirectory(config);
        var existing = Directory.Exists(reportsDir)
            ? Directory.GetFiles(reportsDir, "report-*.csv")
                .Select(Path.GetFileName)
                .Where(f => f is not null)
                .Select(f => f!)
                .OrderDescending()
                .ToList()
            : [];

        return Results.Content(DashboardPage(CompanyName(config), periods, existing, reportsDir, purged), "text/html");
    }

    private static IResult PurgeAsync(IConfiguration config)
    {
        var count = ReportCleanup.PurgeAll(ReportsDirectory(config));
        return Results.Redirect($"/dashboard?purged={count}");
    }

    private static IResult ShowEmployees(TableClient athleteTable, IConfiguration config)
    {
        var athletes = athleteTable.Query<AthleteEntity>()
            .OrderBy(a => a.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Results.Content(EmployeesPage(CompanyName(config), athletes), "text/html");
    }

    private static async Task<IResult> GenerateReportAsync(
        HttpRequest request, ClubConfigStore configStore, TableClient athleteTable, SyncEngine engine, IConfiguration config)
    {
        var form = await request.ReadFormAsync();
        if (!DateOnly.TryParse(form["periodStart"], out var start))
            return Results.BadRequest("Invalid period.");

        var clubConfig = await configStore.GetAsync();
        var current = new PeriodRange(start, start.AddDays(clubConfig.CycleLengthDays - 1));
        var previous = PeriodCalculator.GetPreviousPeriod(clubConfig.CycleAnchor, clubConfig.CycleLengthDays, current);

        var reportsDir = ReportsDirectory(config);
        var fileName = CsvReportWriter.BuildFileName(current);
        var filePath = Path.Combine(reportsDir, fileName);
        var jsonPath = Path.Combine(reportsDir, ReportDetailStore.BuildFileName(current));

        if (!File.Exists(filePath))
        {
            var athletes = athleteTable.Query<TableEntity>().ToList();
            var results = await engine.RunAsync(athletes, current, previous, clubConfig.Points, clubConfig.MaxConcurrency);
            CsvReportWriter.Save(results, filePath);
            ReportDetailStore.Save(results, jsonPath);
        }

        ReportCleanup.Prune(
            reportsDir, DateOnly.FromDateTime(DateTime.UtcNow),
            config.GetValue("Dashboard:CacheWindowDays", 365),
            config.GetValue("Dashboard:ColdGraceDays", 7));

        var csv = await File.ReadAllTextAsync(filePath);
        var detail = ReportDetailStore.Load(jsonPath);
        return Results.Content(ReportPage(CompanyName(config), current, csv, detail, fileName), "text/html");
    }

    private static IResult DownloadReport(string file, IConfiguration config)
    {
        var fileName = Path.GetFileName(file) + ".csv";
        if (ReportCleanup.ParsePeriodEnd(fileName) is null)
            return Results.NotFound();

        var filePath = Path.Combine(ReportsDirectory(config), fileName);
        if (!File.Exists(filePath))
            return Results.NotFound();

        return Results.File(filePath, "text/csv", fileName);
    }

    private static IResult DownloadMarkdown(string file, IConfiguration config)
    {
        var baseName = Path.GetFileName(file);
        var period = ReportCleanup.ParsePeriod(baseName + ".csv");
        if (period is null) return Results.NotFound();

        var jsonPath = Path.Combine(ReportsDirectory(config), baseName + ".json");
        var detail = ReportDetailStore.Load(jsonPath);
        if (detail is null) return Results.NotFound();

        var md = ReportGenerator.BuildMarkdown(detail, CompanyName(config), period.Value.Start.ToString("yyyy-MM-dd"), period.Value.End.ToString("yyyy-MM-dd"));
        return Results.Text(md, "text/markdown", Encoding.UTF8);
    }

    private static async Task<IResult> ShowSettingsAsync(ClubConfigStore configStore, IConfiguration config)
    {
        var clubConfig = await configStore.GetAsync();
        return Results.Content(SettingsPage(CompanyName(config), clubConfig.Points), "text/html");
    }

    private static async Task<IResult> SaveSettingsAsync(HttpRequest request, ClubConfigStore configStore, IConfiguration config)
    {
        var form = await request.ReadFormAsync();
        var clubConfig = await configStore.GetAsync();
        var companyName = CompanyName(config);

        if (!TryParseNonNegativeInt(form["prBonus"], out var prBonus)
            || !TryParseNonNegativeInt(form["periodTotalBonus"], out var periodTotalBonus)
            || !TryParseNonNegativeInt(form["groupBonus"], out var groupBonus)
            || !TryParseNonNegativeInt(form["racePoints"], out var racePoints))
        {
            return Results.Content(SettingsPage(companyName, clubConfig.Points, error: true), "text/html");
        }

        clubConfig.Points = new PointsConfig
        {
            PrBonus = prBonus,
            PeriodTotalBonus = periodTotalBonus,
            GroupBonus = groupBonus,
            RacePoints = racePoints
        };
        await configStore.UpsertAsync(clubConfig);
        InvalidateCurrentPeriod(clubConfig, config);

        return Results.Content(SettingsPage(companyName, clubConfig.Points, saved: true), "text/html");
    }

    // The period still in flux should re-sync under the new rules; already-closed historical periods
    // keep reflecting the rules that were active when they were generated, so they're left alone.
    private static void InvalidateCurrentPeriod(ClubConfig clubConfig, IConfiguration config)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var current = PeriodCalculator.GetPeriod(clubConfig.CycleAnchor, clubConfig.CycleLengthDays, today);

        var reportsDir = ReportsDirectory(config);
        var csvPath = Path.Combine(reportsDir, CsvReportWriter.BuildFileName(current));
        var jsonPath = Path.Combine(reportsDir, ReportDetailStore.BuildFileName(current));

        if (File.Exists(csvPath)) File.Delete(csvPath);
        if (File.Exists(jsonPath)) File.Delete(jsonPath);
    }

    private static bool TryParseNonNegativeInt(string? value, out int result) =>
        int.TryParse(value, out result) && result >= 0;

    private static string CompanyName(IConfiguration config) => config["CompanyName"]!;

    // App Service backs %HOME% with storage that's included free in the plan and persists across
    // restarts/deployments. WEBSITE_SITE_NAME is only set when actually running in App Service, so
    // local `dotnet run` falls back to a folder next to the build output instead of the dev machine's home dir.
    private static string ReportsDirectory(IConfiguration config)
    {
        var isAppService = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME"));
        var home = Environment.GetEnvironmentVariable("HOME");

        var baseDir = isAppService && !string.IsNullOrEmpty(home)
            ? Path.Combine(home, "data")
            : Path.Combine(AppContext.BaseDirectory, "data");

        return Path.Combine(baseDir, "reports");
    }

    private static bool PasswordMatches(string input, string expected)
    {
        var a = Encoding.UTF8.GetBytes(input);
        var b = Encoding.UTF8.GetBytes(expected);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }

    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                    else inQuotes = false;
                }
                else current.Append(c);
            }
            else if (c == '"') inQuotes = true;
            else if (c == ',') { fields.Add(current.ToString()); current.Clear(); }
            else current.Append(c);
        }

        fields.Add(current.ToString());
        return fields;
    }

    private static string PeriodLabel(PeriodRange period) =>
        $"{period.Start:MMM d} – {period.End:MMM d, yyyy}";

    private static string LoginPage(IConfiguration config, bool error = false)
    {
        var companyName = CompanyName(config);
        var banner = error ? Banner("error", "Incorrect password.") : "";

        return TemplateRenderer.RenderPage($"{companyName} Wellness Club Dashboard", "Login.html",
            ("CompanyName", WebUtility.HtmlEncode(companyName)),
            ("ErrorBanner", banner));
    }

    private static string SettingsPage(string companyName, PointsConfig points, bool error = false, bool saved = false)
    {
        var banner = error
            ? Banner("error", "Please enter non-negative whole numbers for all fields.")
            : saved
                ? Banner("success", "Settings saved. The current period's cached report was cleared so it re-syncs with the new values.")
                : "";

        return TemplateRenderer.RenderPage("Scoring settings", "Settings.html",
            ("CompanyName", WebUtility.HtmlEncode(companyName)),
            ("Banner", banner),
            ("PrBonus", points.PrBonus.ToString()),
            ("PeriodTotalBonus", points.PeriodTotalBonus.ToString()),
            ("GroupBonus", points.GroupBonus.ToString()),
            ("RacePoints", points.RacePoints.ToString()));
    }

    private static string EmployeesPage(string companyName, List<AthleteEntity> athletes)
    {
        var rows = athletes.Count == 0
            ? TemplateRenderer.Render("Partials/EmptyTableRow.html",
                ("Colspan", "3"), ("Message", "No employees have connected Strava yet."))
            : TemplateRenderer.RenderEach("Partials/EmployeeRow.html", athletes, a =>
                new (string, string)[]
                {
                    ("Name", WebUtility.HtmlEncode(a.DisplayName)),
                    ("StravaId", WebUtility.HtmlEncode(a.RowKey)),
                    ("LastUpdated", a.Timestamp is { } ts ? ts.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "—")
                });

        var count = $"{athletes.Count} employee{(athletes.Count == 1 ? "" : "s")} connected via Strava.";

        return TemplateRenderer.RenderPage("Registered employees", "Employees.html",
            ("CompanyName", WebUtility.HtmlEncode(companyName)),
            ("Count", count),
            ("Rows", rows));
    }

    private static string DashboardPage(
        string companyName, List<PeriodRange> periods, List<string> existingReports, string reportsDir, int? purged = null)
    {
        var options = TemplateRenderer.RenderEach("Partials/PeriodOption.html", periods, p =>
            new (string, string)[]
            {
                ("Value", p.Start.ToString("yyyy-MM-dd")),
                ("Label", WebUtility.HtmlEncode(PeriodLabel(p)))
            });

        var reportItems = existingReports.Count == 0
            ? TemplateRenderer.Render("Partials/EmptyListItem.html", ("Message", "No saved reports yet."))
            : TemplateRenderer.RenderEach("Partials/SavedReportItem.html", existingReports, f =>
            {
                var baseName = Path.GetFileNameWithoutExtension(f);
                var hasDetail = File.Exists(Path.Combine(reportsDir, baseName + ".json"));
                var mdLink = hasDetail
                    ? TemplateRenderer.Render("Partials/MarkdownLink.html",
                        ("BaseName", WebUtility.UrlEncode(baseName)), ("Label", "Markdown"))
                    : "";

                return new (string, string)[]
                {
                    ("FileName", WebUtility.HtmlEncode(f)),
                    ("BaseName", WebUtility.UrlEncode(baseName)),
                    ("MarkdownLink", mdLink)
                };
            });

        var banner = purged is not null
            ? Banner("success", $"Purged {purged} cached report{(purged == 1 ? "" : "s")}. They'll re-sync from Strava next time they're viewed.")
            : "";

        var purgeButton = existingReports.Count == 0 ? "" : TemplateRenderer.Render("Partials/PurgeButton.html");

        return TemplateRenderer.RenderPage($"{companyName} Wellness Club Dashboard", "Dashboard.html",
            ("CompanyName", WebUtility.HtmlEncode(companyName)),
            ("Banner", banner),
            ("PeriodOptions", options),
            ("SavedReports", reportItems),
            ("PurgeButton", purgeButton));
    }

    private static string ReportPage(string companyName, PeriodRange period, string csv, List<AthleteResult>? detail, string fileName)
    {
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var rows = lines.Skip(1).Select(ParseCsvLine).ToList();

        var headerCells = lines.Length == 0
            ? ""
            : TemplateRenderer.RenderEach("Partials/TableHeaderCell.html", ParseCsvLine(lines[0]), h =>
                new (string, string)[] { ("Text", WebUtility.HtmlEncode(h)) });

        var bodyRows = TemplateRenderer.RenderEach("Partials/TableRow.html", rows, row =>
            new (string, string)[]
            {
                ("Cells", TemplateRenderer.RenderEach("Partials/TableCell.html", row, cell =>
                    new (string, string)[] { ("Text", WebUtility.HtmlEncode(cell)) }))
            });

        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var mdLink = detail is not null
            ? TemplateRenderer.Render("Partials/MarkdownLink.html",
                ("BaseName", WebUtility.UrlEncode(baseName)), ("Label", "Download Markdown"))
            : "";

        return TemplateRenderer.RenderPage("Report", "Report.html",
            ("CompanyName", WebUtility.HtmlEncode(companyName)),
            ("PeriodLabel", WebUtility.HtmlEncode(PeriodLabel(period))),
            ("HeaderCells", headerCells),
            ("BodyRows", bodyRows),
            ("DetailSections", DetailSections(detail)),
            ("FileBaseName", WebUtility.UrlEncode(baseName)),
            ("MarkdownLink", mdLink));
    }

    private static string DetailSections(List<AthleteResult>? results)
    {
        if (results is null) return "";

        var active = results.Where(r => r.TotalPoints > 0).OrderByDescending(r => r.TotalPoints).ToList();
        var excluded = results.Where(r => r.TotalPoints == 0).OrderBy(r => r.DisplayName).ToList();
        var reviewItems = active
            .SelectMany(r => r.Activities.Where(a => a.NeedsReview).Select(a => (r.DisplayName, a)))
            .ToList();

        var sb = new StringBuilder();

        if (excluded.Count > 0)
        {
            sb.Append(SectionHeading("No qualifying activities"));
            sb.Append(TemplateRenderer.Render("Partials/ExcludedAthletes.html",
                ("Names", string.Join(", ", excluded.Select(r => WebUtility.HtmlEncode(r.DisplayName))))));
        }

        sb.Append(SectionHeading("Activity detail"));

        foreach (var r in active)
        {
            var activityRows = TemplateRenderer.RenderEach(
                "Partials/ActivityRow.html", r.Activities.OrderByDescending(a => a.Activity.StartDate), scored =>
            {
                var flag = scored.NeedsReview ? " ⚑" : "";
                var breakdown = string.Join(", ", scored.Reasons);
                var distance = scored.Activity.Distance >= 1000
                    ? $"{scored.Activity.Distance / 1000:F1} km"
                    : scored.Activity.Distance > 0 ? $"{scored.Activity.Distance:F0} m" : "—";
                var time = TimeSpan.FromSeconds(scored.Activity.MovingTime) is var t && t.TotalHours >= 1
                    ? $"{(int)t.TotalHours}h {t.Minutes:D2}m"
                    : $"{t.Minutes}m";

                return new (string, string)[]
                {
                    ("Activity", WebUtility.HtmlEncode(scored.Activity.Name) + flag),
                    ("Date", scored.Activity.StartDate.ToString("yyyy-MM-dd")),
                    ("Type", WebUtility.HtmlEncode(scored.Activity.SportType)),
                    ("Distance", distance),
                    ("Time", time),
                    ("Points", scored.Points.ToString()),
                    ("Breakdown", WebUtility.HtmlEncode(breakdown))
                };
            });

            var bonusRows = TemplateRenderer.RenderEach("Partials/PeriodBonusRow.html", r.PeriodBonuses, bonus =>
                new (string, string)[]
                {
                    ("Points", bonus.Points.ToString()),
                    ("Reason", WebUtility.HtmlEncode(bonus.Reason))
                });

            sb.Append(TemplateRenderer.Render("Partials/AthleteDetailBlock.html",
                ("Name", WebUtility.HtmlEncode(r.DisplayName)),
                ("Points", r.TotalPoints.ToString()),
                ("Rows", activityRows + bonusRows)));
        }

        if (reviewItems.Count > 0)
        {
            sb.Append(SectionHeading("⚑ Group activities to verify"));

            var items = TemplateRenderer.RenderEach("Partials/GroupActivityItem.html", reviewItems, item =>
            {
                var (name, scored) = item;
                return new (string, string)[]
                {
                    ("Name", WebUtility.HtmlEncode(name)),
                    ("Activity", WebUtility.HtmlEncode(scored.Activity.Name)),
                    ("Date", scored.Activity.StartDate.ToString("yyyy-MM-dd"))
                };
            });

            sb.Append(TemplateRenderer.Render("Partials/ListWrapper.html", ("Items", items)));
        }

        return sb.ToString();
    }

    private static string Banner(string cssClass, string message) =>
        TemplateRenderer.Render("Partials/Banner.html", ("Class", cssClass), ("Message", message));

    private static string SectionHeading(string text) =>
        TemplateRenderer.Render("Partials/SectionHeading.html", ("Text", text));
}
