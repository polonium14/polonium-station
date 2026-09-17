using Content.Shared.CCVar;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;

namespace Content.Client._Polonium.Tutorial.Lobby.UI;

/// <summary>
/// How big tutorial bubbles are. A laptop at 125-150% windows scaling has far less room in UI units
/// than a 1440p monitor, so the base size follows the free space and the player's pick scales that.
/// </summary>
public static class TutorialBubbleScale
{
    public const float MinMultiplier = 0.8f;
    public const float MaxMultiplier = 1.25f;

    private const float MinScale = 0.6f;
    private const float MaxScale = 1.25f;

    // at 720 units of height the lobby has nothing to spare, from 1080 up there is plenty
    private const float CrampedHeight = 720f;
    private const float RoomyHeight = 1080f;
    private const float CrampedScale = 0.75f;

    public static float Auto(IUserInterfaceManager ui)
    {
        var height = ui.RootControl.Size.Y;
        if (height <= 0f)
            return 1f;

        var t = Math.Clamp((height - CrampedHeight) / (RoomyHeight - CrampedHeight), 0f, 1f);
        return CrampedScale + (1f - CrampedScale) * t;
    }

    public static float Multiplier(IConfigurationManager cfg)
    {
        return Math.Clamp(cfg.GetCVar(CCVars.TutorialBubbleScale), MinMultiplier, MaxMultiplier);
    }

    public static float Effective(IUserInterfaceManager ui, float multiplier)
    {
        return Math.Clamp(Auto(ui) * multiplier, MinScale, MaxScale);
    }
}
