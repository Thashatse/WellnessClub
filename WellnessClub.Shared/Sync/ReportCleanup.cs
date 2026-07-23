using System.Text.RegularExpressions;

namespace WellnessClub.Shared.Sync;

// Warm periods (ended within cacheWindowDays of today) are kept indefinitely. Once a period falls
// outside that window, any report for it — freshly rebuilt or old — only gets coldGraceDays before
// deletion, since it's outside the normal cache window and not worth keeping long-term.
public static partial class ReportCleanup
{
    public static void Prune(string reportsDirectory, DateOnly today, int cacheWindowDays, int coldGraceDays)
    {
        if (!Directory.Exists(reportsDirectory)) return;

        foreach (var path in Directory.GetFiles(reportsDirectory, "report-*.csv"))
        {
            var periodEnd = ParsePeriodEnd(Path.GetFileName(path));
            if (periodEnd is null) continue;

            var periodAgeDays = today.DayNumber - periodEnd.Value.DayNumber;
            if (periodAgeDays <= cacheWindowDays) continue;

            var fileAgeDays = (DateTime.UtcNow - File.GetLastWriteTimeUtc(path)).TotalDays;
            if (fileAgeDays > coldGraceDays)
            {
                File.Delete(path);

                var jsonSibling = Path.ChangeExtension(path, ".json");
                if (File.Exists(jsonSibling))
                    File.Delete(jsonSibling);
            }
        }
    }

    // Manual override for Prune's age-based rules: wipes every cached report/detail file regardless
    // of period age, so the next view of any period forces a fresh Strava sync. Returns the number
    // of distinct reports (periods) removed, not the raw file count.
    public static int PurgeAll(string reportsDirectory)
    {
        if (!Directory.Exists(reportsDirectory)) return 0;

        var csvFiles = Directory.GetFiles(reportsDirectory, "report-*.csv");

        foreach (var path in csvFiles)
        {
            File.Delete(path);

            var jsonSibling = Path.ChangeExtension(path, ".json");
            if (File.Exists(jsonSibling))
                File.Delete(jsonSibling);
        }

        return csvFiles.Length;
    }

    public static (DateOnly Start, DateOnly End)? ParsePeriod(string fileName)
    {
        var match = FileNameRegex().Match(fileName);
        return match.Success ? (DateOnly.Parse(match.Groups[1].Value), DateOnly.Parse(match.Groups[2].Value)) : null;
    }

    public static DateOnly? ParsePeriodEnd(string fileName) => ParsePeriod(fileName)?.End;

    [GeneratedRegex(@"^report-(\d{4}-\d{2}-\d{2})-(\d{4}-\d{2}-\d{2})\.csv$")]
    private static partial Regex FileNameRegex();
}
