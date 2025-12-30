using App.Services.Diagnostics;
using App.Services.FramePipeline;

namespace App.Application;

public interface ICameraSystem : IAsyncDisposable
{
    IFrameHub Frames { get; }
    ICameraDiagnostics Diagnostics { get; }

    Task StartAsync(CancellationToken ct);
    Task StopAsync(CancellationToken ct);
}
