using Content.Server.Chat.Systems;
using Content.Shared.Chat;
using Content.Shared.Flash;
using Content.Shared.Popups;

namespace Content.Server._Polonium.FlashMessage;

/// <summary>
/// Shows a private popup to the flashed target and performs a public emote on their behalf.
/// Server-only because emotes go through <see cref="ChatSystem"/>, which is server-side.
/// </summary>
public sealed partial class FlashMessageSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ChatSystem _chat = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FlashMessageComponent, AfterFlashedEvent>(OnFlashed);
    }

    private void OnFlashed(Entity<FlashMessageComponent> ent, ref AfterFlashedEvent args)
    {
        // Private "you lost your memory" popup, seen by the target only.
        _popup.PopupEntity(Loc.GetString(ent.Comp.Popup), args.Target, args.Target);

        if (!ent.Comp.DoEmote)
            return;

        // Reaction emote, seen by everyone nearby. ignoreActionBlocker because the
        // target is mid-flash and would otherwise be blocked from emoting.
        _chat.TrySendInGameICMessage(
            args.Target,
            Loc.GetString(ent.Comp.Emote),
            InGameICChatType.Emote,
            hideChat: false,
            ignoreActionBlocker: true);
    }
}
