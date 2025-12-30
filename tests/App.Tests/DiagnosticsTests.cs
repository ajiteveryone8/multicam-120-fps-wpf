using App.Domain;
using App.Infrastructure.Timing;
using App.Services.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace App.Tests;

public class DiagnosticsTests
{
    [Fact]
    public void Diagnostics_TracksFramesAndDrops()
    {
        var clock = new MonotonicClock();
        var diag = new CameraDiagnostics(clock, new NullLogger<CameraDiagnostics>());

        var cam = CameraId.From("CAM-1");
        var meta1 = new FrameMetadata(cam, 1, DateTime.UtcNow.Ticks, clock.GetTimestampQpc(), 10, 10, PixelFormat.Bgra32);
        diag.OnFrameCaptured(meta1);
        diag.OnFrameDropped(cam);

        var snap = diag.GetSnapshot(cam);
        Assert.Equal(cam, snap.CameraId);
        Assert.True(snap.FramesCaptured >= 1);
        Assert.True(snap.DroppedFrames >= 1);
    }
}
