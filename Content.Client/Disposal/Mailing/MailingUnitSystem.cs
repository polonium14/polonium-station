using Content.Shared.Disposal.Components;
using Content.Shared.Disposal.Mailing;

namespace Content.Client.Disposal.Mailing;

public sealed partial class MailingUnitSystem : SharedMailingUnitSystem
{
    [Dependency] private SharedUserInterfaceSystem _userInterface = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MailingUnitComponent, AfterAutoHandleStateEvent>(OnMailingState);
    }

    private void OnMailingState(Entity<MailingUnitComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        // Polonium: the open window can be the device view, which reads the component by itself
        if (_userInterface.TryGetOpenUi(ent.Owner, MailingUnitUiKey.Key, out var open) && open is MailingUnitBoundUserInterface bui)
        {
            bui.Refresh(ent);
        }
    }
}
