using App.Domain;

namespace App.Services.FramePipeline;

public interface ICameraPipelineFactory
{
    CameraFramePipeline Create(CameraId cameraId);
}
