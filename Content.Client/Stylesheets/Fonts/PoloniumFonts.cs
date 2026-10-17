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

    public static string GetFallbackPath(string primaryPath)
    {
        var bold = primaryPath.Contains("Bold");
        var italic = primaryPath.Contains("Italic");
        var kind = bold && italic ? "BoldItalic" : bold ? "Bold" : italic ? "Italic" : "Regular";
        return $"/Fonts/NotoSans/NotoSans-{kind}.ttf";
    }

    public static Font GetFontWithFallback(IResourceCache cache, string path, int size)
    {
        return cache.GetFont([path, GetFallbackPath(path)], size);
    }

    public static void HijackFontTags(IResourceCache cache, IPrototypeManager prototypes, FontTagHijackHolder holder)
    {
        holder.Hijack = (protoId, size) =>
        {
            if (!prototypes.TryIndex(protoId, out var prototype))
                prototype = prototypes.Index<FontPrototype>(FontTag.DefaultFont);

#pragma warning disable CS0618
            return GetFontWithFallback(cache, prototype.Path.ToString(), size);
#pragma warning restore CS0618
        };
    }
}
