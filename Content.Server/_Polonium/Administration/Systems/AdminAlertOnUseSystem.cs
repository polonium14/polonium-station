using Content.Server.Administration.Components;
using Content.Server.Chat.Managers;
using Content.Shared.Interaction.Events;

namespace Content.Server._Polonium.Administration.Systems;

public sealed class AdminAlertOnUseSystem : EntitySystem
{
    [Dependency] private readonly IChatManager _chatManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AdminAlertOnUseComponent, UseInHandEvent>(OnUseInHand);
    }

    public void OnUseInHand(EntityUid uid, AdminAlertOnUseComponent component, UseInHandEvent args)
    {
        _chatManager.SendAdminAlert(args.User, component.Message == string.Empty ? Loc.GetString("admin-alert-on-use-default-message", ("item", ToPrettyString(uid))) : component.Message);
    }
}
