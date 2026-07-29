using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Player;

namespace Content.Shared._RMC14.Xenonids.Announce;

// stub for now - real hive chat comes later
public sealed class SharedXenoAnnounceSystem : EntitySystem
{
    public void AnnounceToHive(
        EntityUid source,
        EntityUid hive,
        string message,
        SoundSpecifier? sound = null,
        PopupType? popup = null,
        Color? color = null)
    {
    }

    public void AnnounceSameHive(
        EntityUid xeno,
        string message,
        SoundSpecifier? sound = null,
        PopupType? popup = null,
        Color? color = null)
    {
    }
}
