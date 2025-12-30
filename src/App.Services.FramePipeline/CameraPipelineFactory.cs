using App.Domain;
using App.Services.Diagnostics;
using Microsoft.Extensions.Logging;

namespace App.Services.FramePipeline;

public sealed class CameraPipelineFactory : ICameraPipelineFactory
{
    private readonly FrameHub _hub;
    private readonly ICameraDiagnostics _diag;
    private readonly ILoggerFactory _logFactory;

    public CameraPipelineFactory(ICameraDiagnostics diag, ILoggerFactory logFactory)
    {
        _diag = diag;
        _logFactory = logFactory;
        _hub = new FrameHub(_diag);
    }

    public FrameHub Hub => _hub;

    public CameraFramePipeline Create(CameraId cameraId)
        => new(cameraId, _hub, _diag, _logFactory.CreateLogger<CameraFramePipeline>());
}
