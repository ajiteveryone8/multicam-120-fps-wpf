using App.Common.Options;

namespace App.Infrastructure.Cameras.Abstractions;

public interface ICameraFactory
{
    ICameraDevice Create(CameraProfileOptions profile);
}
