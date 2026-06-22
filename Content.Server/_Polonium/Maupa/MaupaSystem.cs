// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Chat;
using Content.Server.Chat.Systems;

namespace Content.Server._Polonium.Maupa;

public sealed class MaupaSystem : EntitySystem
{
    [Dependency] private readonly AutoEmoteSystem _autoEmote = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MaupaComponent, ComponentStartup>(OnInit);
    }

    private void OnInit(EntityUid uid, MaupaComponent component, ComponentStartup args)
    {

        EnsureComp<AutoEmoteComponent>(uid);
        _autoEmote.AddEmote(uid, "MaupaScream");
    }
}
