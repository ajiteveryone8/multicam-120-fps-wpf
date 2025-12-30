namespace App.Infrastructure.Timing;

public interface IMonotonicClock
{
    long GetTimestampQpc();
    long Frequency { get; }
    double ToMilliseconds(long deltaQpc);
}
