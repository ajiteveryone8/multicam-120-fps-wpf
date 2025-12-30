using System.Diagnostics;

namespace App.Infrastructure.Timing;

public sealed class MonotonicClock : IMonotonicClock
{
    public long Frequency => Stopwatch.Frequency;

    public long GetTimestampQpc() => Stopwatch.GetTimestamp();

    public double ToMilliseconds(long deltaQpc) => (deltaQpc * 1000.0) / Frequency;
}
