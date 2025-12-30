namespace App.Domain;

/// <summary>
/// Deterministic timestamp model: capture timestamp is set as close to camera delivery as possible,
/// using a monotonic clock (QPC-backed) in infrastructure.
/// </summary>
public sealed record FrameMetadata(
    CameraId CameraId,
    long Sequence,
    long CaptureTimestampTicks,
    long CaptureTimestampQpc,
    int Width,
    int Height,
    PixelFormat PixelFormat);
