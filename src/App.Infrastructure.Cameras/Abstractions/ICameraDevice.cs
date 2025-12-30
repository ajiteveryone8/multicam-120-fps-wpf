using System.Buffers;
using App.Domain;

namespace App.Infrastructure.Cameras.Abstractions;

public interface ICameraDevice : IAsyncDisposable
{
    CameraId CameraId { get; }

    /// <summary>Starts capture loop. Implementations must be non-blocking and isolate internal failures.</summary>
    Task StartAsync(ICameraFrameSink sink, CancellationToken ct);

    /// <summary>Requests capture to stop and releases device resources.</summary>
    Task StopAsync(CancellationToken ct);
}

public interface ICameraFrameSink
{
    ValueTask OnFrameAsync(RawFrame frame, CancellationToken ct);
    ValueTask OnCameraFaultAsync(CameraId cameraId, Exception ex, CancellationToken ct);
}

public sealed class RawFrame : IAsyncDisposable
{
    public required FrameMetadata Metadata { get; init; }
    public required IMemoryOwner<byte> BufferOwner { get; init; }
    public required int StrideBytes { get; init; }

    public ReadOnlyMemory<byte> Buffer => BufferOwner.Memory;

    public ValueTask DisposeAsync()
    {
        BufferOwner.Dispose();
        return ValueTask.CompletedTask;
    }
}
