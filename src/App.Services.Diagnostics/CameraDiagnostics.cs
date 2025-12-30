using System.Collections.Concurrent;
using App.Domain;
using App.Infrastructure.Timing;
using Microsoft.Extensions.Logging;

namespace App.Services.Diagnostics;

public sealed class CameraDiagnostics : ICameraDiagnostics
{
    private sealed class State
    {
        public long LastQpc;
        public long WorstDeltaQpc;
        public long Frames;
        public long Dropped;
        public HealthState Health = HealthState.Ok;
        public string? Message;
        public double SmoothedFps;
    }

    private readonly ConcurrentDictionary<CameraId, State> _map = new();
    private readonly IMonotonicClock _clock;
    private readonly ILogger _log;

    public CameraDiagnostics(IMonotonicClock clock, ILogger<CameraDiagnostics> log)
    {
        _clock = clock;
        _log = log;
    }

    public void OnFrameCaptured(in FrameMetadata meta)
    {
        var s = _map.GetOrAdd(meta.CameraId, _ => new State());
        var last = Interlocked.Exchange(ref s.LastQpc, meta.CaptureTimestampQpc);

        if (last != 0)
        {
            var delta = meta.CaptureTimestampQpc - last;
            if (delta > 0)
            {
                // worst-case delta
                long curWorst;
                do
                {
                    curWorst = Volatile.Read(ref s.WorstDeltaQpc);
                    if (delta <= curWorst) break;
                }
                while (Interlocked.CompareExchange(ref s.WorstDeltaQpc, delta, curWorst) != curWorst);

                // fps EMA (very light)
                var instFps = _clock.Frequency / (double)delta;
                var prev = s.SmoothedFps;
                s.SmoothedFps = prev == 0 ? instFps : (prev * 0.9 + instFps * 0.1);
            }
        }

        Interlocked.Increment(ref s.Frames);

        if (s.Health == HealthState.Disconnected)
            s.Health = HealthState.Ok;
    }

    public void OnFrameDropped(CameraId cameraId)
    {
        var s = _map.GetOrAdd(cameraId, _ => new State());
        Interlocked.Increment(ref s.Dropped);

        // heuristics: excessive drops => degraded
        if (Volatile.Read(ref s.Dropped) > 50)
            s.Health = HealthState.Degraded;
    }

    public void OnCameraFault(CameraId cameraId, Exception ex)
    {
        var s = _map.GetOrAdd(cameraId, _ => new State());
        s.Health = HealthState.Faulted;
        s.Message = ex.Message;
        _log.LogError(ex, "Camera fault: {CameraId}", cameraId);
    }

    public CameraHealth GetSnapshot(CameraId cameraId)
    {
        var s = _map.GetOrAdd(cameraId, _ => new State());
        var worstMs = _clock.ToMilliseconds(Volatile.Read(ref s.WorstDeltaQpc));
        var frames = Volatile.Read(ref s.Frames);
        var drops = Volatile.Read(ref s.Dropped);
        var fps = s.SmoothedFps;

        return new CameraHealth(
            CameraId: cameraId,
            State: s.Health,
            Message: s.Message,
            Fps: fps,
            WorstFrameDeltaMs: worstMs,
            DroppedFrames: drops,
            FramesCaptured: frames,
            TimestampUtc: DateTimeOffset.UtcNow);
    }

    public IReadOnlyList<CameraHealth> GetAllSnapshots()
        => _map.Keys.OrderBy(k => k.Value).Select(GetSnapshot).ToList();
}
