// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Popups;
using Content.Shared.Verbs;
using System.Linq;

namespace Content.Shared._Polonium.Paper;

public sealed partial class SignatureWriterSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SignatureWriterComponent, ComponentInit>(OnCompInit);
        SubscribeLocalEvent<SignatureWriterComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAltVerbs);
    }

    private void OnCompInit(EntityUid uid, SignatureWriterComponent comp, ref ComponentInit args)
    {
        if (comp.ColorList.Count >= 1)
        {
            comp.Color = comp.ColorList.First().Value;
            Dirty(uid, comp);
        }
    }

    private void OnGetAltVerbs(EntityUid uid, SignatureWriterComponent comp, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;

        // Font selection
        if (comp.FontList.Count >= 2)
        {
            var priority = 0;

            foreach (var entry in comp.FontList)
            {
                AlternativeVerb selection = new()
                {
                    Text = entry.Key,
                    Category = FontSelect,
                    Priority = priority,
                    Act = () =>
                    {
                        // Resolve the component at execution time so a stale reference
                        // cannot be used after the entity/component has been removed.
                        if (!TryComp<SignatureWriterComponent>(uid, out var signatureComp))
                            return;

                        signatureComp.Font = entry.Value;
                        Dirty(uid, signatureComp);
                        _popup.PopupEntity(Loc.GetString("signature-writer-component-font-set", ("font", entry.Key)), user, user);
                    }
                };

                priority--;
                args.Verbs.Add(selection);
            }
        }

        // Color selection
        if (comp.ColorList.Count >= 2)
        {
            var priority = 0;

            foreach (var entry in comp.ColorList)
            {
                AlternativeVerb selection = new()
                {
                    Text = entry.Key,
                    Category = ColorSelect,
                    Priority = priority,
                    Act = () =>
                    {
                        // Resolve the component at execution time so a stale reference
                        // cannot be used after the entity/component has been removed.
                        if (!TryComp<SignatureWriterComponent>(uid, out var signatureComp))
                            return;

                        signatureComp.Color = entry.Value;
                        Dirty(uid, signatureComp);
                        _popup.PopupEntity(Loc.GetString("signature-writer-component-color-set", ("color", entry.Key)), user, user);
                    }
                };

                priority--;
                args.Verbs.Add(selection);
            }
        }
    }

    private static readonly VerbCategory FontSelect = new("verb-categories-signature-font-select", null);

    private static readonly VerbCategory ColorSelect = new("verb-categories-signature-color-select", null);
}
