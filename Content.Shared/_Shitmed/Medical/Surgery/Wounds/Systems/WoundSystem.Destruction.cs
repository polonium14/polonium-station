using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Robust.Shared.Audio;

namespace Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;

public sealed partial class WoundSystem
{
    public void DestroyWoundable(EntityUid woundable, WoundableComponent? component = null)
    {
        if (!Resolve(woundable, ref component) || component.WoundableSeverity == WoundableSeverity.Severed)
            return;

        SeverWoundable(woundable, component, component.WoundableDestroyedSound);
    }

    /// <summary>
    /// Surgical/clean removal — same end state as <see cref="DestroyWoundable"/>, distinct
    /// entry point for Surgery Steps (a later phase) to call without implying violence.
    /// </summary>
    public void AmputateWoundableSafely(EntityUid woundable, WoundableComponent? component = null)
    {
        if (!Resolve(woundable, ref component) || component.WoundableSeverity == WoundableSeverity.Severed)
            return;

        SeverWoundable(woundable, component, component.WoundableDelimbedSound);
    }

    private void SeverWoundable(EntityUid woundable, WoundableComponent component, SoundSpecifier sound)
    {
        var old = component.WoundableSeverity;
        component.WoundableSeverity = WoundableSeverity.Severed;
        Dirty(woundable, component);

        _audio.PlayPvs(sound, woundable);

        var evt = new WoundableSeverityChangedEvent(woundable, old, WoundableSeverity.Severed);
        RaiseLocalEvent(woundable, ref evt);

        SyncTargetingBodyStatus(woundable);
    }
}
