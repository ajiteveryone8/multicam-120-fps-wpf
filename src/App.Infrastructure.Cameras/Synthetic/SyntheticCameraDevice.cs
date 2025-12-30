using System.Buffers;
using System.Diagnostics;
using App.Common;
using App.Common.Options;
using App.Domain;
using App.Infrastructure.Cameras.Abstractions;
using App.Infrastructure.Timing;
using Microsoft.Extensions.Logging;

namespace App.Infrastructure.Cameras.Synthetic;

/// <summary>
/// Deterministic synthetic source to validate pipeline capacity without vendor SDKs.
/// Produces BGRA32 frames at requested FPS with monotonic timestamps.
/// </summary>
public sealed class SyntheticCameraDevice : ICameraDevice
{
    private readonly CameraProfileOptions _profile;
    private readonly IMonotonicClock _clock;
    private readonly ILogger _log;

    private Task? _loop;
    private CancellationTokenSource? _cts;
    private volatile bool _running;

    public CameraId CameraId { get; }

    public SyntheticCameraDevice(CameraProfileOptions profile, IMonotonicClock clock, ILogger<SyntheticCameraDevice> log)
    {
        _profile = Guard.NotNull(profile, nameof(profile));
        _clock = Guard.NotNull(clock, nameof(clock));
        _log = log;
        CameraId = CameraId.From(profile.CameraId);
    }

    public Task StartAsync(ICameraFrameSink sink, CancellationToken ct)
    {
        if (_running) return Task.CompletedTask;
        _running = true;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var token = _cts.Token;

        _loop = Task.Run(async () =>
        {
            try
            {
                var width = Guard.InRange(_profile.Width, 16, 8192, nameof(_profile.Width));
                var height = Guard.InRange(_profile.Height, 16, 8192, nameof(_profile.Height));
                var fps = Guard.InRange(_profile.TargetFps, 1, 500, nameof(_profile.TargetFps));
                var fmt = ParsePixelFormat(_profile.PixelFormat);
                if (fmt != PixelFormat.Bgra32) throw new NotSupportedException("Synthetic camera currently emits BGRA32 only.");

                var bpp = fmt.BytesPerPixel();
                var stride = width * bpp;
                var bytes = stride * height;

                long seq = 0;
                var frameIntervalQpc = (long)Math.Round(_clock.Frequency / (double)fps);

                var next = _clock.GetTimestampQpc();

                while (!token.IsCancellationRequested)
                {
                    // deterministic schedule (best-effort) using monotonic clock
                    var now = _clock.GetTimestampQpc();
                    if (now < next)
                    {
                        var waitMs = _clock.ToMilliseconds(next - now);
                        if (waitMs > 1) await Task.Delay(TimeSpan.FromMilliseconds(waitMs * 0.75), token).ConfigureAwait(false);
                        continue;
                    }

                    var owner = MemoryPool<byte>.Shared.Rent(bytes);
                    var mem = owner.Memory.Slice(0, bytes);
                    FillTestPattern(mem.Span, width, height, stride, seq);

                    var meta = new FrameMetadata(
                        CameraId: CameraId,
                        Sequence: seq++,
                        CaptureTimestampTicks: DateTime.UtcNow.Ticks,
                        CaptureTimestampQpc: now,
                        Width: width,
                        Height: height,
                        PixelFormat: fmt);

                    var raw = new RawFrame { Metadata = meta, BufferOwner = owner, StrideBytes = stride };

                    await sink.OnFrameAsync(raw, token).ConfigureAwait(false);

                    next += frameIntervalQpc;

                    // drift correction: if we're far behind, resync to avoid unbounded catch-up.
                    var behind = _clock.GetTimestampQpc() - next;
                    if (behind > _clock.Frequency / 2)
                        next = _clock.GetTimestampQpc();
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                try { await sink.OnCameraFaultAsync(CameraId, ex, CancellationToken.None).ConfigureAwait(false); }
                catch { /* swallow: fault isolation */ }
                _log.LogError(ex, "Synthetic camera loop faulted: {CameraId}", CameraId);
            }
        }, token);

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken ct)
    {
        if (!_running) return;
        _running = false;

        try
        {
            _cts?.Cancel();
            if (_loop != null) await _loop.WaitAsync(TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
        }
        catch { /* shutdown is best-effort */ }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            _loop = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try { await StopAsync(CancellationToken.None).ConfigureAwait(false); }
        catch { }
    }

    private static PixelFormat ParsePixelFormat(string s)
        => s.Trim().Equals("BGRA32", StringComparison.OrdinalIgnoreCase) ? PixelFormat.Bgra32
         : s.Trim().Equals("GRAY8", StringComparison.OrdinalIgnoreCase) ? PixelFormat.Gray8
         : PixelFormat.Bgra32;

    private static void FillTestPattern(Span<byte> dst, int width, int height, int stride, long seq)
    {
        // Simple moving gradient + crosshair: deterministic and cheap.
        int t = (int)(seq % 255);
        int cx = width / 2, cy = height / 2;

        for (int y = 0; y < height; y++)
        {
            var row = dst.Slice(y * stride, stride);
            for (int x = 0; x < width; x++)
            {
                int i = x * 4;
                byte v = (byte)((x + y + t) & 0xFF);

                bool cross = (x == cx || y == cy);
                row[i + 0] = (byte)(cross ? 0 : v);        // B
                row[i + 1] = (byte)(cross ? 0 : (255 - v)); // G
                row[i + 2] = (byte)(cross ? 255 : v);       // R
                row[i + 3] = 255;                           // A
            }
        }
    }
}
