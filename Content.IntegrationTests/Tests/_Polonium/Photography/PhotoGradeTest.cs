using Content.Client._Polonium.Photography;
using Content.Shared._Polonium.Photography;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.IntegrationTests.Tests._Polonium.Photography;

/// <summary>
/// Checks the analog film grade does its four visible things - warm tint, lifted blacks,
/// vignette, determinism - so a look regression is caught without a live client to eyeball.
/// </summary>
[TestOf(typeof(PhotoGrade))]
public sealed class PhotoGradeTest
{
    private const int Size = PhotographyConstants.PhotoSizePixels;
    private const int Center = Size / 2;

    [Test]
    public void WarmsNeutralTonesTowardRed()
    {
        var graded = PhotoGrade.Apply(new Rgba32(128, 128, 128, 255), Center, Center);
        Assert.That(graded.R, Is.GreaterThan(graded.B), "A warm grade pushes red above blue on a neutral input.");
    }

    [Test]
    public void LiftsBlacks()
    {
        var graded = PhotoGrade.Apply(new Rgba32(0, 0, 0, 255), Center, Center);
        Assert.That(graded.R, Is.GreaterThan(0), "Faded film never reaches pure black.");
    }

    [Test]
    public void VignetteDarkensCorners()
    {
        var center = PhotoGrade.Apply(new Rgba32(255, 255, 255, 255), Center, Center);
        var corner = PhotoGrade.Apply(new Rgba32(255, 255, 255, 255), 0, 0);
        Assert.That(corner.R, Is.LessThan(center.R), "Corners are darker than the center.");
    }

    [Test]
    public void IsDeterministic()
    {
        var a = PhotoGrade.Apply(new Rgba32(60, 120, 200, 255), 40, 90);
        var b = PhotoGrade.Apply(new Rgba32(60, 120, 200, 255), 40, 90);
        Assert.That(a, Is.EqualTo(b), "The same pixel grades identically every time (no RNG).");
    }
}
