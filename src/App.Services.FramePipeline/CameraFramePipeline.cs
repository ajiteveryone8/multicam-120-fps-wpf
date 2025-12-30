using App.Domain;
using App.Infrastructure.Cameras.Abstractions;
using App.Services.Diagnostics;
using Microsoft.Extensions.Logging;

namespace App.Services.FramePipeline;

/// <summary>
/// Per-camera low-latency pipeline with explicit back-pressure semantics:
/// - Capture thread stores the newest frame in a single-slot mailbox
/// - If a newer frame arrives before the consumer picks up the old one, the old frame is dropped (disposed)
/// This guarantees bounded memory and deterministic latency (always prefer newest).
/// </summary>
public sealed class CameraFramePipeline : ICameraFrameSink, IAsyncDisposable
{
    private readonly CameraId _cameraId;
    private readonly FrameHub _hub;
    private readonly ICameraDiagnostics _diag;
    private readonly ILogger _log;

    private readonly SemaphoreSlim _signal = new(0, int.MaxValue);
    private readonly object _gate = new();

    private RawFrame? _mailbox;
    private Task? _consumer;
    private CancellationTokenSource? _cts;

    public CameraFramePipeline(CameraId cameraId, FrameHub hub, ICameraDiagnostics diag, ILogger<CameraFramePipeline> log)
    {
        _cameraId = cameraId;
        _hub = hub;
        _diag = diag;
        _log = log;

        _hub.EnsureCameraRegistered(cameraId);
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        _consumer = Task.Run(async () =>
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await _signal.WaitAsync(ct).ConfigureAwait(false);

                    RawFrame? next = null;
                    lock (_gate)
                    {
                        next = _mailbox;
                        _mailbox = null;
                    }

                    if (next is null) continue;

                    try
                    {
                        var latest = new LatestFrame
                        {
                            Metadata = next.Metadata,
                            BufferOwner = next.BufferOwner,
                            StrideBytes = next.StrideBytes
                        };

                        await _hub.PublishAsync(latest).ConfigureAwait(false);
                        // ownership moved; do not dispose next's owner here.
                    }
                    catch (Exception ex)
                    {
                        try { await next.DisposeAsync().ConfigureAwait(false); } catch { }
                        _log.LogError(ex, "Pipeline processing fault: {CameraId}", _cameraId);
                        _diag.OnCameraFault(_cameraId, ex);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _log.LogError(ex, "Pipeline consumer fault: {CameraId}", _cameraId);
                _diag.OnCameraFault(_cameraId, ex);
            }
        }, ct);
    }

    public async ValueTask OnFrameAsync(RawFrame frame, CancellationToken ct)
    {
        RawFrame? dropped = null;

        lock (_gate)
        {
            if (_mailbox is not null)
            {
                dropped = _mailbox;
                _mailbox = frame;
            }
            else
            {
                _mailbox = frame;
                _signal.Release();
                return;
            }
        }

        // We dropped the previous frame in favor of newest.
        _diag.OnFrameDropped(_cameraId);
        if (dropped is not null)
            await dropped.DisposeAsync().ConfigureAwait(false);

        // Signal consumer (it may already be signaled, but mailbox swap requires another wake-up).
        _signal.Release();
    }

    public ValueTask OnCameraFaultAsync(CameraId cameraId, Exception ex, CancellationToken ct)
    {
        _diag.OnCameraFault(cameraId, ex);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            _cts?.Cancel();
            _signal.Release();
            if (_consumer != null) await _consumer.ConfigureAwait(false);
        }
        catch { }
        finally
        {
            RawFrame? pending;
            lock (_gate) { pending = _mailbox; _mailbox = null; }
            if (pending is not null)
            {
                try { await pending.DisposeAsync().ConfigureAwait(false); } catch { }
            }

            _cts?.Dispose();
            _cts = null;
            _signal.Dispose();
        }
    }
}
