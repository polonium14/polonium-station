using Content.Client._Polonium.Photography;
using Content.Shared._Polonium.Photography;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.IntegrationTests.Tests._Polonium.Photography;

/// <summary>
/// Guards the wire-format invariant the security model rests on: a captured photo is ALWAYS
/// exactly <see cref="PhotographyConstants.PhotoByteLength"/> bytes of RGB565, so the server
/// validates by length and never runs an image decoder.
/// </summary>
[TestOf(typeof(PhotoCodec))]
public sealed class PhotoCodecTest
{
    private const int Size = PhotographyConstants.PhotoSizePixels;

    [Test]
    public void PackedIsAlwaysFixedLength()
    {
        using var img = new Image<Rgba32>(Size, Size, new Rgba32(123, 45, 67, 255));
        Assert.That(PhotoCodec.ToRgb565(img), Has.Length.EqualTo(PhotographyConstants.PhotoByteLength));
    }

    [Test]
    public void UnpackYieldsFullPixelCount()
    {
        var blob = new byte[PhotographyConstants.PhotoByteLength];
        Assert.That(PhotoCodec.ToPixels(blob), Has.Length.EqualTo(PhotographyConstants.PhotoPixelCount));
    }

    /// <summary>
    /// The RGB565 endpoints decode to full-scale channels (big-endian on the wire).
    /// </summary>
    [Test]
    public void EndpointsDecodeToFullScale()
    {
        Assert.Multiple(() =>
        {
            Assert.That(FirstPixel(0xF8, 0x00), Is.EqualTo(new Rgba32(255, 0, 0, 255)), "0xF800 = pure red.");
            Assert.That(FirstPixel(0x07, 0xE0), Is.EqualTo(new Rgba32(0, 255, 0, 255)), "0x07E0 = pure green.");
            Assert.That(FirstPixel(0x00, 0x1F), Is.EqualTo(new Rgba32(0, 0, 255, 255)), "0x001F = pure blue.");
            Assert.That(FirstPixel(0xFF, 0xFF), Is.EqualTo(new Rgba32(255, 255, 255, 255)), "0xFFFF = white.");
            Assert.That(FirstPixel(0x00, 0x00), Is.EqualTo(new Rgba32(0, 0, 0, 255)), "0x0000 = black.");
        });
    }

    private static Rgba32 FirstPixel(byte hi, byte lo)
    {
        var blob = new byte[PhotographyConstants.PhotoByteLength];
        blob[0] = hi;
        blob[1] = lo;
        return PhotoCodec.ToPixels(blob)[0];
    }
}
