using System.Numerics;
using Content.Client._Polonium.Photography;
using Content.Shared._Polonium.Photography;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Polonium.Photography;

/// <summary>
/// Covers the crop-window geometry that decides where a photo is cut out of the full render.
/// It's the one geometry-sensitive piece of the render path integration tests can't reach
/// (Draw never runs headless), so a clamp regression here would slip through as an
/// out-of-bounds read.
/// </summary>
[TestOf(typeof(PhotographyCaptureControl))]
public sealed class PhotoCropTest
{
    private const int Crop = PhotographyConstants.PhotoSizePixels; // 128
    private const int Half = Crop / 2;

    [Test]
    public void CentredTargetCentresTheWindow()
    {
        var origin = PhotographyCaptureControl.CropOrigin(new Vector2(256, 256), 512, 512);
        Assert.That(origin, Is.EqualTo(new Vector2i(256 - Half, 256 - Half)));
    }

    [Test]
    public void EdgeTargetsClampInsideTheImage()
    {
        Assert.Multiple(() =>
        {
            Assert.That(PhotographyCaptureControl.CropOrigin(new Vector2(0, 0), 512, 512),
                Is.EqualTo(Vector2i.Zero));
            Assert.That(PhotographyCaptureControl.CropOrigin(new Vector2(512, 512), 512, 512),
                Is.EqualTo(new Vector2i(512 - Crop, 512 - Crop)));
        });
    }

    [Test]
    public void ImageSmallerThanCropNeverGoesNegative()
    {
        var origin = PhotographyCaptureControl.CropOrigin(new Vector2(10, 10), 100, 100);
        Assert.That(origin, Is.EqualTo(Vector2i.Zero));
    }
}
