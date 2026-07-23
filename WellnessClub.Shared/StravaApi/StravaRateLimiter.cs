using System.Threading.RateLimiting;

namespace WellnessClub.Shared.StravaApi;

// Strava's granted limits: 600 requests/15min overall, 300 requests/15min for read endpoints.
// Read calls draw from both windows since they count toward the overall cap too.
public class StravaRateLimiter(int overallPerWindow = 600, int readPerWindow = 300, int windowMinutes = 15) : IDisposable
{
    private readonly RateLimiter _overall = new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
    {
        PermitLimit = overallPerWindow,
        Window = TimeSpan.FromMinutes(windowMinutes),
        SegmentsPerWindow = windowMinutes,
        QueueLimit = int.MaxValue,
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
    });

    private readonly RateLimiter _read = new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
    {
        PermitLimit = readPerWindow,
        Window = TimeSpan.FromMinutes(windowMinutes),
        SegmentsPerWindow = windowMinutes,
        QueueLimit = int.MaxValue,
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
    });

    public Task AcquireOverallAsync(CancellationToken ct = default) => AcquireAsync(_overall, ct);

    public async Task AcquireReadAsync(CancellationToken ct = default)
    {
        await AcquireOverallAsync(ct);
        await AcquireAsync(_read, ct);
    }

    private static async Task AcquireAsync(RateLimiter limiter, CancellationToken ct)
    {
        using var lease = await limiter.AcquireAsync(1, ct);
        if (!lease.IsAcquired)
            throw new InvalidOperationException("Strava rate limit lease could not be acquired.");
    }

    public void Dispose()
    {
        _overall.Dispose();
        _read.Dispose();
    }
}
