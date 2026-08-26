using Robust.Shared.Maths;

namespace Content.Shared._Polonium.Photography;

/// <summary>Fixed geometry of a captured photo, in one place because client (renders/bitpacks) and server (validates payload length) must agree exactly.</summary>
public static class PhotographyConstants
{
    /// <summary>Region captured, in tiles, per side.</summary>
    public const int TilesPerSide = 4;

    /// <summary>Rendered pixels per tile. Matches <c>EyeManager.PixelsPerMeter</c>.</summary>
    public const int PixelsPerTile = 32;

    /// <summary>Width/height of the captured image in pixels (square).</summary>
    public const int PhotoSizePixels = TilesPerSide * PixelsPerTile; // 128

    /// <summary>Total pixels in a photo.</summary>
    public const int PhotoPixelCount = PhotoSizePixels * PhotoSizePixels; // 16384

    /// <summary>Bytes per pixel on the wire: RGB565 packed color.</summary>
    public const int BytesPerPixel = 2;

    /// <summary>
    /// Photo length in bytes: one RGB565 value per pixel, big-endian. Server rejects any
    /// submission whose Data.Length differs, removing the malformed-image / decompression-bomb
    /// class since the payload is raw fixed-size color data and no image decoder ever runs.
    /// </summary>
    public const int PhotoByteLength = PhotoPixelCount * BytesPerPixel; // 32768

    // Flashlight params shared by server world burst and client capture light so the photo
    // matches the burst bystanders saw. Point source at camera: ~3-tile reach, high energy.
    public const float FlashRadius = 3f;
    public const float FlashEnergy = 6f;
    public static readonly Color FlashColor = Color.FromHex("#F2F5FF");

    /// <summary>Lifetime of the server-side world burst light, seconds.</summary>
    public const float FlashBurstLifetime = 0.3f;

    /// <summary>Range (tiles) the flash blinds when it fires.</summary>
    public const float FlashBlindRange = 4f;

    /// <summary>Max aim distance (tiles). Generous so line-of-sight is the real limit; just stops absurd cross-map clicks.</summary>
    public const float PhotoMaxRange = 15f;
}
