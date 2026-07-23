using WellnessClub.Shared.Models;

namespace WellnessClub.Shared.Sync;

public class ReportGenerator
{
    public void PrintAndSave(List<AthleteResult> results, string companyName, string periodStart, string periodEnd)
    {
        var active = results.Where(r => r.TotalPoints > 0).OrderByDescending(r => r.TotalPoints).ToList();
        var excluded = results.Where(r => r.TotalPoints == 0).OrderBy(r => r.DisplayName).ToList();

        Console.WriteLine();
        Console.WriteLine($"╔══════════════════════════════════════════════════════════╗");
        Console.WriteLine($"║      {companyName.ToUpperInvariant()} WELLNESS CLUB — {periodStart} to {periodEnd}      ║");
        Console.WriteLine($"╠══════════════════════════════════════════════════════════╣");
        Console.WriteLine($"║  #  {"Athlete",-28} {"Points",6}  {"Activities",10}  ║");
        Console.WriteLine($"╠══════════════════════════════════════════════════════════╣");

        for (int i = 0; i < active.Count; i++)
        {
            var r = active[i];
            var medal = i switch { 0 => "🥇", 1 => "🥈", 2 => "🥉", _ => $" {i + 1} " };
            Console.WriteLine($"║ {medal}  {r.DisplayName,-28} {r.TotalPoints,6}  {r.Activities.Count,10}  ║");
        }

        Console.WriteLine($"╚══════════════════════════════════════════════════════════╝");

        if (excluded.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("○ No qualifying activities: " + string.Join(", ", excluded.Select(r => r.DisplayName)));
        }

        var reviewItems = active
            .SelectMany(r => r.Activities.Where(a => a.NeedsReview).Select(a => (r.DisplayName, a)))
            .ToList();

        if (reviewItems.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("⚑ GROUP ACTIVITIES TO VERIFY (confirm a colleague was involved):");
            foreach (var (name, scored) in reviewItems)
                Console.WriteLine($"  • {name} — {scored.Activity.Name} on {scored.Activity.StartDate:yyyy-MM-dd}");
        }

        var path = $"report-{periodStart}-{periodEnd}.md";
        File.WriteAllText(path, BuildMarkdown(results, companyName, periodStart, periodEnd));
        Console.WriteLine($"\nReport saved to {path}");
    }

    public static string BuildMarkdown(List<AthleteResult> results, string companyName, string start, string end)
    {
        var active = results.Where(r => r.TotalPoints > 0).OrderByDescending(r => r.TotalPoints).ToList();
        var excluded = results.Where(r => r.TotalPoints == 0).OrderBy(r => r.DisplayName).ToList();
        var reviewItems = active
            .SelectMany(r => r.Activities.Where(a => a.NeedsReview).Select(a => (r.DisplayName, a)))
            .ToList();

        var lines = new List<string>
        {
            $"# {companyName} Wellness Club — {start} to {end}",
            "",
            "## Leaderboard",
            "",
            "| # | Athlete | Points | Activities | Distance | Time |",
            "|---|---------|--------|------------|----------|------|"
        };

        for (int i = 0; i < active.Count; i++)
        {
            var r = active[i];
            var medal = i switch { 0 => "🥇", 1 => "🥈", 2 => "🥉", _ => $"{i + 1}" };
            var totalDist = r.Activities.Sum(a => a.Activity.Distance);
            var totalTime = TimeSpan.FromSeconds(r.Activities.Sum(a => a.Activity.MovingTime));
            var distStr = $"{totalDist / 1000:F1} km";
            var timeStr = totalTime.TotalHours >= 1 ? $"{(int)totalTime.TotalHours}h {totalTime.Minutes:D2}m" : $"{totalTime.Minutes}m";
            lines.Add($"| {medal} | {r.DisplayName} | {r.TotalPoints} | {r.Activities.Count} | {distStr} | {timeStr} |");
        }

        if (excluded.Count > 0)
        {
            lines.Add("");
            lines.Add("### No qualifying activities");
            foreach (var r in excluded)
                lines.Add($"- {r.DisplayName}");
        }

        // ── Activity detail (second 'page') ──────────────────────────────────
        lines.Add("");
        lines.Add("---");
        lines.Add("");
        lines.Add("## Activity Detail");

        foreach (var r in active)
        {
            lines.Add("");
            lines.Add($"### {r.DisplayName} — {r.TotalPoints} pts");
            lines.Add("");
            lines.Add("| Activity | Date | Type | Distance | Time | Points | Breakdown |");
            lines.Add("|----------|------|------|----------|------|--------|-----------|");

            foreach (var scored in r.Activities.OrderByDescending(a => a.Activity.StartDate))
            {
                var flag = scored.NeedsReview ? " ⚑" : "";
                var breakdown = string.Join(", ", scored.Reasons);
                var distance = scored.Activity.Distance >= 1000
                    ? $"{scored.Activity.Distance / 1000:F1} km"
                    : scored.Activity.Distance > 0 ? $"{scored.Activity.Distance:F0} m" : "—";
                var time = TimeSpan.FromSeconds(scored.Activity.MovingTime) is var t && t.TotalHours >= 1
                    ? $"{(int)t.TotalHours}h {t.Minutes:D2}m"
                    : $"{t.Minutes}m";
                lines.Add($"| {scored.Activity.Name}{flag} | {scored.Activity.StartDate:yyyy-MM-dd} | {scored.Activity.SportType} | {distance} | {time} | {scored.Points} | {breakdown} |");
            }

            foreach (var bonus in r.PeriodBonuses)
                lines.Add($"| *(Period bonus)* | — | — | — | — | {bonus.Points} | {bonus.Reason} |");

            var totalDist = r.Activities.Sum(a => a.Activity.Distance);
            var totalTime = TimeSpan.FromSeconds(r.Activities.Sum(a => a.Activity.MovingTime));
            var totalDistStr = totalDist > 0 ? $"{totalDist / 1000:F1} km" : "—";
            var totalTimeStr = totalTime.TotalHours >= 1 ? $"{(int)totalTime.TotalHours}h {totalTime.Minutes:D2}m" : $"{totalTime.Minutes}m";
            lines.Add($"| **Total** | | | **{totalDistStr}** | **{totalTimeStr}** | **{r.TotalPoints}** | |");
        }

        if (reviewItems.Count > 0)
        {
            lines.Add("");
            lines.Add("---");
            lines.Add("");
            lines.Add("## ⚑ Group Activities to Verify");
            lines.Add("");
            lines.Add("Confirm a colleague was involved before awarding the group bonus.");
            lines.Add("");
            foreach (var (name, scored) in reviewItems)
                lines.Add($"- **{name}** — {scored.Activity.Name} ({scored.Activity.StartDate:yyyy-MM-dd})");
        }

        return string.Join('\n', lines);
    }
}
