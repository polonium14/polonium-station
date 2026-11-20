using Content.Client._Polonium.UserInterface.PixelUi;
using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;

namespace Content.Client._Polonium.Kitchen.UI;

public sealed partial class MicrowaveDoor : Control
{
    [Dependency] private IResourceCache _cache = default!;

    public const int OpenWidth = 12;

    private const int ClosedLeft = 14;
    private const int OpenLeft = 5;
    private const int EdgeWidth = 7;

    private const int CrackReach = 136;

    private static readonly Vector2i CrackCentre = new(118, 74);

    private readonly Texture _closed;
    private readonly Texture _open;
    private readonly Texture _cracks;

    public float Open = 1f;

    /// <summary>
    /// How far the cracks have spread over the glass of the closed door, from 0 to 1.
    /// </summary>
    public float Cracks;

    public MicrowaveDoor()
    {
        IoCManager.InjectDependencies(this);

        _closed = _cache.GetTexture(MicrowaveDeviceWindow.TexturePath + "door.png");
        _open = _cache.GetTexture(MicrowaveDeviceWindow.TexturePath + "door_open.png");
        _cracks = _cache.GetTexture(MicrowaveDeviceWindow.TexturePath + "cracks.png");

        MouseFilter = MouseFilterMode.Ignore;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var canvas = new PixelCanvas(handle, UIScale);
        if (Open >= 1f)
        {
            canvas.Texture(_open, 0, 0);
            return;
        }

        if (Open <= 0f)
        {
            canvas.Texture(_closed, ClosedLeft, 0);
            if (Cracks <= 0f)
                return;

            var reach = (int) (Cracks * CrackReach);
            var shown = new UIBox2(
                Math.Max(0, CrackCentre.X - reach),
                Math.Max(0, CrackCentre.Y - reach),
                Math.Min(_cracks.Width, CrackCentre.X + reach),
                Math.Min(_cracks.Height, CrackCentre.Y + reach));
            canvas.Region(_cracks, shown, ClosedLeft + (int) shown.Left, (int) shown.Top);
            return;
        }

        var swing = Open * Open;
        var left = ClosedLeft + (int) MathF.Round((OpenLeft - ClosedLeft) * swing);
        var width = _closed.Width + (int) MathF.Round((EdgeWidth - _closed.Width) * swing);
        canvas.Stretch(_closed, UIBox2.FromDimensions(0f, 0f, _closed.Width, _closed.Height), left, 0, width, _closed.Height);
    }
}
