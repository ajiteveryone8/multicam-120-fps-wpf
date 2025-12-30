using App.Domain;

namespace App.Services.Diagnostics;

public interface ICameraDiagnostics
{
    void OnFrameCaptured(in FrameMetadata meta);
    void OnFrameDropped(CameraId cameraId);
    void OnCameraFault(CameraId cameraId, Exception ex);
    CameraHealth GetSnapshot(CameraId cameraId);
    IReadOnlyList<CameraHealth> GetAllSnapshots();
}
