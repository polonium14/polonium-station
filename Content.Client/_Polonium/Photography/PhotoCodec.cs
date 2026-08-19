using Content.Shared._Polonium.Photography;
using Robust.Client.Utility;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client._Polonium.Photography;

/// <summary>Converts between a rendered <see cref="Rgba32"/> image and the compact RGB565 wire format (<see cref="PhotographyConstants.PhotoByteLength"/> bytes): one 16-bit color per pixel, big-endian, row-major, no palette. The fixed size is the point - the server validates by length and never runs an image decoder.</summary>
public static class PhotoCodec
{
    private const int Size = PhotographyConstants.PhotoSizePixels;

    /// <summary>Pack the image to RGB565.</summary>
    public static byte[] ToRgb565(Image<Rgba32> image)
    {
        var src = image.GetPixelSpan();
        var packed = new byte[PhotographyConstants.PhotoByteLength];

        var count = Size * Size;
        for (var i = 0; i < count; i++)
        {
            var p = PhotoGrade.Apply(src[i], i % Size, i / Size);
            var value = (ushort) (((p.R >> 3) << 11) | ((p.G >> 2) << 5) | (p.B >> 3));
            packed[i * 2] = (byte) (value >> 8);
            packed[i * 2 + 1] = (byte) (value & 0xFF);
        }

        return packed;
    }

    /// <summary>Unpack a validated RGB565 blob back into an <see cref="Rgba32"/> buffer for display.</summary>
    public static Rgba32[] ToPixels(byte[] packed)
    {
        var pixels = new Rgba32[Size * Size];

        for (var i = 0; i < pixels.Length; i++)
        {
            var value = (ushort) ((packed[i * 2] << 8) | packed[i * 2 + 1]);

            var r5 = (value >> 11) & 0x1F;
            var g6 = (value >> 5) & 0x3F;
            var b5 = value & 0x1F;

            var r = (byte) ((r5 << 3) | (r5 >> 2));
            var g = (byte) ((g6 << 2) | (g6 >> 4));
            var b = (byte) ((b5 << 3) | (b5 >> 2));

            pixels[i] = new Rgba32(r, g, b, 255);
        }

        return pixels;
    }
}
