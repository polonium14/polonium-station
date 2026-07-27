// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Emoting;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Emoting;

public sealed partial class AnimatedEmotesSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AnimatedEmotesComponent, EmoteEvent>(OnEmote);
    }

    private void OnEmote(EntityUid uid, AnimatedEmotesComponent component, ref EmoteEvent args)
    {
        // Every emote in the game raises this, but only a handful animate.
        if (args.Emote.Event == null)
            return;

        PlayEmoteAnimation(uid, args.Emote.ID);
    }

    public void PlayEmoteAnimation(EntityUid uid, ProtoId<EmotePrototype> prot)
    {
        RaiseNetworkEvent(new AnimatedEmoteEvent(GetNetEntity(uid), prot), Filter.Pvs(uid));
    }
}
