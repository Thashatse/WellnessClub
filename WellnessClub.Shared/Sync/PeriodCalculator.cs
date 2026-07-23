using WellnessClub.Shared.Models;

namespace WellnessClub.Shared.Sync;

public static class PeriodCalculator
{
    // anchor is the Friday a cycle starts counting from; cycles are back-to-back, cycleLengthDays long.
    public static PeriodRange GetPeriod(DateOnly anchor, int cycleLengthDays, DateOnly referenceDate)
    {
        var daysSinceAnchor = referenceDate.DayNumber - anchor.DayNumber;
        var cycleIndex = (int)Math.Floor(daysSinceAnchor / (double)cycleLengthDays);
        var start = anchor.AddDays(cycleIndex * cycleLengthDays);
        var end = start.AddDays(cycleLengthDays - 1);
        return new PeriodRange(start, end);
    }

    public static PeriodRange GetPreviousPeriod(DateOnly anchor, int cycleLengthDays, PeriodRange current) =>
        GetPeriod(anchor, cycleLengthDays, current.Start.AddDays(-1));

    // Newest-first list of every completed/current cycle from the anchor up to (and including) upTo's cycle.
    public static List<PeriodRange> ListPeriods(DateOnly anchor, int cycleLengthDays, DateOnly upTo)
    {
        var periods = new List<PeriodRange>();
        var current = GetPeriod(anchor, cycleLengthDays, upTo);

        while (current.Start >= anchor)
        {
            periods.Add(current);
            current = new PeriodRange(current.Start.AddDays(-cycleLengthDays), current.End.AddDays(-cycleLengthDays));
        }

        return periods;
    }
}
