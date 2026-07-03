namespace WellnessClub.Sync.Services;

public record AthleteResult(string DisplayName, int TotalPoints, List<ScoredActivity> Activities);

public class ReportGenerator
{
    public void PrintAndSave(List<AthleteResult> results, string periodStart, string periodEnd)
    {
        var sorted = results.OrderByDescending(r => r.TotalPoints).ToList();

        Console.WriteLine();
        Console.WriteLine($"╔══════════════════════════════════════════════════════════╗");
        Console.WriteLine($"║      PAYMENOW WELLNESS CLUB — {periodStart} to {periodEnd}      ║");
        Console.WriteLine($"╠══════════════════════════════════════════════════════════╣");
        Console.WriteLine($"║  #  {"Athlete",-28} {"Points",6}  {"Activities",10}  ║");
        Console.WriteLine($"╠══════════════════════════════════════════════════════════╣");

        for (int i = 0; i < sorted.Count; i++)
        {
            var r = sorted[i];
            var medal = i switch { 0 => "🥇", 1 => "🥈", 2 => "🥉", _ => $" {i + 1} " };
            Console.WriteLine($"║ {medal}  {r.DisplayName,-28} {r.TotalPoints,6}  {r.Activities.Count,10}  ║");
        }

        Console.WriteLine($"╚══════════════════════════════════════════════════════════╝");

        var reviewItems = sorted
            .SelectMany(r => r.Activities.Where(a => a.NeedsReview).Select(a => (r.DisplayName, a)))
            .ToList();

        if (reviewItems.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("⚑ GROUP ACTIVITIES TO VERIFY (confirm PMN colleague was involved):");
            foreach (var (name, scored) in reviewItems)
                Console.WriteLine($"  • {name} — {scored.Activity.Name} on {scored.Activity.StartDate:yyyy-MM-dd}");
        }

        SaveMarkdown(sorted, periodStart, periodEnd, reviewItems);
    }

    private static void SaveMarkdown(
        List<AthleteResult> results, string start, string end,
        List<(string DisplayName, ScoredActivity Activity)> reviewItems)
    {
        var path = $"report-{start}-{end}.md";
        var lines = new List<string>
        {
            $"# Paymenow Wellness Club — {start} to {end}",
            "",
            "| # | Athlete | Points | Activities |",
            "|---|---------|--------|------------|"
        };

        for (int i = 0; i < results.Count; i++)
        {
            var r = results[i];
            lines.Add($"| {i + 1} | {r.DisplayName} | {r.TotalPoints} | {r.Activities.Count} |");
        }

        lines.Add("");
        lines.Add("## Activity Breakdown");

        foreach (var r in results)
        {
            lines.Add($"### {r.DisplayName}");
            foreach (var scored in r.Activities.OrderByDescending(a => a.Points))
            {
                var flag = scored.NeedsReview ? " ⚑" : "";
                lines.Add($"- **{scored.Activity.Name}** ({scored.Activity.StartDate:yyyy-MM-dd}) — {scored.Points} pts{flag}");
                foreach (var reason in scored.Reasons)
                    lines.Add($"  - {reason}");
            }
        }

        if (reviewItems.Count > 0)
        {
            lines.Add("");
            lines.Add("## ⚑ Group Activities to Verify");
            foreach (var (name, scored) in reviewItems)
                lines.Add($"- {name} — {scored.Activity.Name} ({scored.Activity.StartDate:yyyy-MM-dd})");
        }

        File.WriteAllLines(path, lines);
        Console.WriteLine($"\nReport saved to {path}");
    }
}
