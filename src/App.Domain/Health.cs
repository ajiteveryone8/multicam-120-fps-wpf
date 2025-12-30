namespace App.Domain;

public enum HealthState
{
    Ok,
    Degraded,
    Faulted,
    Disconnected
}

public sealed record CameraHealth(
    CameraId CameraId,
    HealthState State,
    string? Message,
    double Fps,
    double WorstFrameDeltaMs,
    long DroppedFrames,
    long FramesCaptured,
    DateTimeOffset TimestampUtc);
