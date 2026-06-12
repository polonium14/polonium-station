using Content.Client.Resources;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;

namespace Content.Client.Stylesheets.Fonts;

/// <summary>
///     Polonium window title font
/// </summary>
[PublicAPI]
public static class TomorrowFont
{
    public const string Path = "/Fonts/_Polonium/Tomorrow/Tomorrow-Bold.ttf";

    public const int WindowTitleSize = 13;

    public static Font GetWindowTitle(IResourceCache resCache) => resCache.GetFont(Path, WindowTitleSize);
}
