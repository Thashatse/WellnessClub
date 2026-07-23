using System.Text.Json;
using WellnessClub.Shared.Models;

namespace WellnessClub.Shared.Sync;

// Persists the full per-athlete/per-activity detail as a JSON sidecar next to the CSV summary, so the
// dashboard can render the same activity-level breakdown the console app's Markdown report has, even
// on a cache hit (when there's no live sync in flight to source it from).
public static class ReportDetailStore
{
    public static string BuildFileName(PeriodRange period) =>
        $"report-{period.Start:yyyy-MM-dd}-{period.End:yyyy-MM-dd}.json";

    public static void Save(List<AthleteResult> results, string filePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, JsonSerializer.Serialize(results));
    }

    public static List<AthleteResult>? Load(string filePath) =>
        File.Exists(filePath) ? JsonSerializer.Deserialize<List<AthleteResult>>(File.ReadAllText(filePath)) : null;
}
