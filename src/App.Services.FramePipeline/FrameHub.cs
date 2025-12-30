using System.Collections.Concurrent;
using App.Domain;
using App.Services.Diagnostics;

namespace App.Services.FramePipeline;

public sealed class FrameHub : IFrameHub
{
    private readonly ConcurrentDictionary<CameraId, LatestFrame?> _latest = new();
    private readonly ICameraDiagnostics _diag;

    public FrameHub(ICameraDiagnostics diag)
    {
        _diag = diag;
    }

    public IReadOnlyList<CameraId> Cameras => _latest.Keys.OrderBy(k => k.Value).ToList();

    public bool TryGetLatest(CameraId cameraId, out LatestFrame frame)
    {
        frame = default!;
        if (!_latest.TryGetValue(cameraId, out var cur) || cur is null) return false;
        frame = cur;
        return true;
    }

    internal async ValueTask PublishAsync(LatestFrame frame)
    {
        _diag.OnFrameCaptured(frame.Metadata);

        var old = _latest.AddOrUpdate(frame.Metadata.CameraId, frame, (_, prev) => frame);
        if (old != null && !ReferenceEquals(old, frame))
        {
            await old.DisposeAsync().ConfigureAwait(false);
        }
    }

    internal void EnsureCameraRegistered(CameraId cameraId)
        => _latest.TryAdd(cameraId, null);
}
