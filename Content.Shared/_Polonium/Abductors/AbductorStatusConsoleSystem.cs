using Content.Shared.Popups;
using Content.Shared.UserInterface;

namespace Content.Shared._Polonium.Abductors;

public sealed partial class AbductorStatusConsoleSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AbductorStatusConsoleComponent, ActivatableUIOpenAttemptEvent>(OnOpenAttempt);
    }

    private void OnOpenAttempt(Entity<AbductorStatusConsoleComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled || HasComp<AbductorComponent>(args.User))
            return;

        args.Cancel();
        if (!args.Silent)
            _popup.PopupEntity(Loc.GetString("abductor-unfamiliar"), ent, args.User);
    }
}
