using App.Common.Options;
using App.Domain;
using App.Infrastructure.Cameras.Abstractions;
using App.Infrastructure.Timing;
using App.Services.Diagnostics;
using App.Services.FramePipeline;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace App.Application;

/// <summary>
/// Application-layer orchestrator: owns camera lifecycles and per-camera pipelines.
/// Presentation consumes only IFrameHub + diagnostics snapshots.
/// </summary>
public sealed class CameraSystem : ICameraSystem
{
    private readonly AppOptions _opts;
    private readonly ICameraFactory _factory;
    private readonly IMonotonicClock _clock;
    private readonly ILogger _log;

    private readonly ICameraPipelineFactory _pipelineFactory;
    private readonly IFrameHub _hub;
    private readonly ICameraDiagnostics _diag;

    private readonly List<ICameraDevice> _devices = new();
    private readonly List<CameraFramePipeline> _pipelines = new();

    private volatile bool _running;

    public IFrameHub Frames => _hub;
    public ICameraDiagnostics Diagnostics => _diag;

    public CameraSystem(
        IOptions<AppOptions> options,
        ICameraFactory factory,
        IMonotonicClock clock,
        ICameraDiagnostics diag,
        ICameraPipelineFactory pipelineFactory,
        IFrameHub hub,
        ILogger<CameraSystem> log)
    {
        _opts = options.Value;
        _factory = factory;
        _clock = clock;
        _diag = diag;
        _pipelineFactory = pipelineFactory;
        _hub = hub;
        _log = log;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        if (_running) return;
        _running = true;

        if (_opts.CameraSystem.Cameras.Count == 0)
            throw new InvalidOperationException("No cameras configured. See appsettings.json -> App:CameraSystem:Cameras.");

        foreach (var cam in _opts.CameraSystem.Cameras)
        {
            var id = CameraId.From(cam.CameraId);

            var pipeline = _pipelineFactory.Create(id);
            pipeline.Start();
            _pipelines.Add(pipeline);

            var device = _factory.Create(cam);
            _devices.Add(device);

            await device.StartAsync(pipeline, ct).ConfigureAwait(false);

            _log.LogInformation("Started camera {CameraId} provider={Provider} {W}x{H}@{Fps}",
                cam.CameraId, cam.Provider, cam.Width, cam.Height, cam.TargetFps);
        }
    }

    public async Task StopAsync(CancellationToken ct)
    {
        if (!_running) return;
        _running = false;

        foreach (var d in _devices)
        {
            try { await d.StopAsync(ct).ConfigureAwait(false); } catch (Exception ex) { _log.LogWarning(ex, "StopAsync failed"); }
        }

        foreach (var p in _pipelines)
        {
            try { await p.DisposeAsync().ConfigureAwait(false); } catch { }
        }

        foreach (var d in _devices)
        {
            try { await d.DisposeAsync().ConfigureAwait(false); } catch { }
        }

        _devices.Clear();
        _pipelines.Clear();
    }

    public async ValueTask DisposeAsync()
    {
        try { await StopAsync(CancellationToken.None).ConfigureAwait(false); } catch { }
    }
}
