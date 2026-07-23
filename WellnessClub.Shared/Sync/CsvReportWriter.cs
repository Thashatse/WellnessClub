using System.Globalization;
using System.Text;
using WellnessClub.Shared.Models;

namespace WellnessClub.Shared.Sync;

public static class CsvReportWriter
{
    public static string BuildFileName(PeriodRange period) =>
        $"report-{period.Start:yyyy-MM-dd}-{period.End:yyyy-MM-dd}.csv";

    public static string BuildContent(List<AthleteResult> results)
    {
        var active = results.Where(r => r.TotalPoints > 0).OrderByDescending(r => r.TotalPoints).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("Rank,Athlete,Points,Activities,DistanceKm,Time");

        for (var i = 0; i < active.Count; i++)
        {
            var r = active[i];
            var totalDistKm = r.Activities.Sum(a => a.Activity.Distance) / 1000;
            var totalTime = TimeSpan.FromSeconds(r.Activities.Sum(a => a.Activity.MovingTime));

            sb.AppendLine(string.Join(",",
                i + 1,
                Escape(r.DisplayName),
                r.TotalPoints,
                r.Activities.Count,
                totalDistKm.ToString("F1", CultureInfo.InvariantCulture),
                Escape(FormatTime(totalTime))));
        }

        return sb.ToString();
    }

    public static void Save(List<AthleteResult> results, string filePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, BuildContent(results));
    }

    private static string FormatTime(TimeSpan t) =>
        t.TotalHours >= 1 ? $"{(int)t.TotalHours}h {t.Minutes:D2}m" : $"{t.Minutes}m";

    private static string Escape(string value) =>
        value.Contains(',') || value.Contains('"')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
