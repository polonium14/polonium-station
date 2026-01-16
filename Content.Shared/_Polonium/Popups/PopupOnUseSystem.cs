// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Interaction.Events;
using Content.Shared.Popups.Components;
using Content.Shared.Popups;
using Robust.Shared.Timing;

namespace Content.Shared.Popups;

public sealed class PopupOnUseSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PopupOnUseComponent, UseInHandEvent>(OnUseInHand);
    }

    private void OnUseInHand(EntityUid uid, PopupOnUseComponent component, UseInHandEvent args)
    {
        if (_gameTiming.CurTime < component.LastUsed + TimeSpan.FromSeconds(PopupOnUseComponent.UseDelaySeconds))
            return;

        component.LastUsed = _gameTiming.CurTime;

        var popupSize = component.PopupSize switch
        {
            "large" => PopupType.Large,
            "small" => PopupType.Small,
            _ => PopupType.Medium
        };

        if (!component.ShowToOthers)
        {
            _popup.PopupClient(
                component.Message,
                args.User,
                popupSize);
        }
        else
        {
            _popup.PopupPredicted(
                component.Message,
                args.User,
                args.User,
                popupSize);
        }
    }
}
