using System.Text.Json.Serialization;

namespace App.Common.Options;

public sealed class AppOptions
{
    public string Environment { get; init; } = "Dev";
    public DiagnosticsOptions Diagnostics { get; init; } = new();
    public CameraSystemOptions CameraSystem { get; init; } = new();
}

public sealed class DiagnosticsOptions
{
    public int UiUpdateHz { get; init; } = 10;
    public int HealthPublishHz { get; init; } = 5;
}

public sealed class CameraSystemOptions
{
    public List<CameraProfileOptions> Cameras { get; init; } = new();
}

public sealed class CameraProfileOptions
{
    public string CameraId { get; init; } = "CAM-1";
    public string Provider { get; init; } = "Synthetic"; // Synthetic, DirectShow, VendorSdkX (future)
    public int Width { get; init; } = 640;
    public int Height { get; init; } = 480;
    public int TargetFps { get; init; } = 120;
    public string PixelFormat { get; init; } = "BGRA32";
}
