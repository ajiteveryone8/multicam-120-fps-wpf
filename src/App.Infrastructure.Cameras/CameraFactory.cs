using App.Common.Options;
using App.Infrastructure.Cameras.Abstractions;
using App.Infrastructure.Cameras.Synthetic;
using App.Infrastructure.Timing;
using Microsoft.Extensions.Logging;

namespace App.Infrastructure.Cameras;

public sealed class CameraFactory : ICameraFactory
{
    private readonly IMonotonicClock _clock;
    private readonly ILoggerFactory _logFactory;

    public CameraFactory(IMonotonicClock clock, ILoggerFactory logFactory)
    {
        _clock = clock;
        _logFactory = logFactory;
    }

    public ICameraDevice Create(CameraProfileOptions profile)
    {
        // Provider selection is explicit and future-proof: new providers can be added without touching UI.
        return profile.Provider.Trim().Equals("Synthetic", StringComparison.OrdinalIgnoreCase)
            ? new SyntheticCameraDevice(profile, _clock, _logFactory.CreateLogger<SyntheticCameraDevice>())
            : throw new NotSupportedException($"Camera provider '{profile.Provider}' not implemented.");
    }
}
