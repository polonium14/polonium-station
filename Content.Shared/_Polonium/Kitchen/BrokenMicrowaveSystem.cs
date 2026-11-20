using Content.Shared.Kitchen.Components;
using Content.Shared.Popups;
using Content.Shared.UserInterface;

namespace Content.Shared._Polonium.Kitchen;

/// <summary>
/// A broken microwave does not open its window any more.
/// </summary>
public sealed partial class BrokenMicrowaveSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;

    [SubscribeLocalEvent]
    private void OnOpenAttempt(Entity<MicrowaveComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled || !ent.Comp.Broken)
            return;

        args.Cancel();
        if (!args.Silent)
            _popup.PopupEntity(Loc.GetString("microwave-component-interact-using-broken"), ent, args.User);
    }
}
