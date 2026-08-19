using System;
using Content.Shared._Polonium.Photography;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client._Polonium.Photography;

/// <summary>Cheap "analog film" color grade applied per pixel before RGB565 quantization: desaturation, warm tint, lifted/compressed blacks, soft vignette, deterministic grain. Purely cosmetic and client-side.</summary>
public static class PhotoGrade
{
    private const int Size = PhotographyConstants.PhotoSizePixels;

    private const float Saturation = 0.82f;    // 1 = untouched, 0 = grayscale
    private const float WarmR = 1.07f;
    private const float WarmG = 1.01f;
    private const float WarmB = 0.90f;
    private const float Contrast = 0.88f;      // <1 softens
    private const float Lift = 14f;            // Raised black floor, in 0..255
    private const float Vignette = 0.55f;      // Corner darkening strength (0 = none)
    private const float Grain = 6f;            // Peak grain amplitude, in 0..255

    private static readonly float CenterX = (Size - 1) / 2f;
    private static readonly float CenterY = (Size - 1) / 2f;

    /// <summary>Grade one pixel at (<paramref name="x"/>, <paramref name="y"/>).</summary>
    public static Rgba32 Apply(Rgba32 p, int x, int y)
    {
        float r = p.R, g = p.G, b = p.B;

        var luma = 0.299f * r + 0.587f * g + 0.114f * b;
        r = luma + (r - luma) * Saturation;
        g = luma + (g - luma) * Saturation;
        b = luma + (b - luma) * Saturation;

        r *= WarmR;
        g *= WarmG;
        b *= WarmB;

        r = (r - 128f) * Contrast + 128f + Lift;
        g = (g - 128f) * Contrast + 128f + Lift;
        b = (b - 128f) * Contrast + 128f + Lift;

        var dx = (x - CenterX) / CenterX;
        var dy = (y - CenterY) / CenterY;
        var vignette = 1f - Vignette * (dx * dx + dy * dy) * 0.5f;
        r *= vignette;
        g *= vignette;
        b *= vignette;

        var grain = (Hash(x, y) / 255f - 0.5f) * Grain;
        r += grain;
        g += grain;
        b += grain;

        return new Rgba32(Clamp(r), Clamp(g), Clamp(b), 255);
    }

    private static byte Clamp(float v)
    {
        return (byte) Math.Clamp(v, 0f, 255f);
    }

    private static int Hash(int x, int y)
    {
        var h = (x * 73856093) ^ (y * 19349663);
        h ^= h >> 13;
        return h & 0xFF;
    }
}
