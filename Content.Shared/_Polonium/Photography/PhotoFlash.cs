using Robust.Shared.GameObjects;

namespace Content.Shared._Polonium.Photography;

/// <summary>Shared point-light setup for the flash, so server world burst and client capture light stay visually identical.</summary>
public static class PhotoFlash
{
    public static void Configure(SharedPointLightSystem lights, EntityUid light)
    {
        lights.EnsureLight(light);
        lights.SetRadius(light, PhotographyConstants.FlashRadius);
        lights.SetEnergy(light, PhotographyConstants.FlashEnergy);
        lights.SetColor(light, PhotographyConstants.FlashColor);
        lights.SetEnabled(light, true);
    }
}
