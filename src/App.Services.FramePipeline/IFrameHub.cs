using System.Buffers;
using App.Domain;

namespace App.Services.FramePipeline;

/// <summary>
/// Single-writer (pipelines) / multi-reader (UI, streaming, CV) access to latest frames per camera.
/// Returned frames are immutable snapshots owned by the hub.
/// </summary>
public interface IFrameHub
{
    bool TryGetLatest(CameraId cameraId, out LatestFrame frame);
    IReadOnlyList<CameraId> Cameras { get; }
}

public sealed class LatestFrame : IAsyncDisposable
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
