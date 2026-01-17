// SPDX-FileCopyrightText: 2026 Damian Zieliński <zientasek.pl@gmail.com>
// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.RegularExpressions;
using Content.Server.Speech.Components;
using Robust.Shared.Random;

namespace Content.Server.Speech.EntitySystems;

public sealed class MalopolskiAccentSystem : EntitySystem
{
    private static readonly Regex RegexDz = new(@"dź\b");


    [Dependency] private readonly IRobustRandom _random = default!;


    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MalopolskiAccentComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(EntityUid uid, MalopolskiAccentComponent component, AccentGetEvent args)
    {
        var message = args.Message;

        // idźże
        message = RegexDz.Replace(message, "dźże");

        // czeba być wyczymałym
        message = message.Replace("Trz", "Cz").Replace("trz", "cz");


        args.Message = message;
    }
}
