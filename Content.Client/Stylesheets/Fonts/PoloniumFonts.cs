using Content.Client.Resources;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;

namespace Content.Client.Stylesheets.Fonts;

[PublicAPI]
public static class PoloniumFonts
{
    public static readonly ProtoId<FontPrototype> WindowTitleFont = "Tomorrow";

    public const int WindowTitleSize = 13;

    public static Font GetWindowTitleFont(IResourceCache cache, int size = WindowTitleSize)
    {
        var prototypes = IoCManager.Resolve<IPrototypeManager>();
        return cache.GetFont(prototypes, WindowTitleFont, size);
    }
}
