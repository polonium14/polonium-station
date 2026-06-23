// SPDX-FileCopyrightText: 2026 Damian Zieliński <zientasek.pl@gmail.com>
// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.RegularExpressions;
using Content.Server.Speech.Components;
using Robust.Shared.Random;

namespace Content.Server.Speech.EntitySystems;

public sealed class WiejskiAccentSystem : EntitySystem
{
    private static readonly Regex RegexScie1 = new(@"ście\b");
    private static readonly Regex RegexScie2 = new(@"scie\b");
    private static readonly Regex RegexDzcie1 = new(@"dźcie\b");
    private static readonly Regex RegexDzcie2 = new(@"dźcie\b");
    private static readonly Regex RegexAjcie = new(@"ajcie\b");
    private static readonly Regex RegexRzcie = new(@"rzcie\b");
    private static readonly Regex RegexAl = new(@"ał\b");
    private static readonly Regex RegexAla = new(@"ała\b");
    private static readonly Regex RegexAlo = new(@"ało\b"); // neutratyw
    private static readonly Regex RegexAlu = new(@"ału\b"); // dukaizm
    private static readonly Regex RegexAles = new(@"ałeś\b");
    private static readonly Regex RegexAlas = new(@"ałaś\b");
    private static readonly Regex RegexAlos = new(@"ałoś\b"); // neutratyw
    private static readonly Regex RegexAlus = new(@"ałuś\b"); // dukaizm
    private static readonly Regex RegexAlem = new(@"ałem\b");
    private static readonly Regex RegexAlam = new(@"ałam\b");
    private static readonly Regex RegexAlom = new(@"ałom\b"); // neutratyw
    private static readonly Regex RegexAlum = new(@"ałum\b"); // dukaizm


    [Dependency] private readonly IRobustRandom _random = default!;


    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WiejskiAccentComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(EntityUid uid, WiejskiAccentComponent component, AccentGetEvent args)
    {
        var message = args.Message;


        // Powiedzieliśta
        message = RegexScie1.Replace(message, "śta");
        message = RegexScie2.Replace(message, "śta");

        // chodźcie -> choćta
        message = RegexDzcie1.Replace(message, "ćta");
        message = RegexDzcie2.Replace(message, "ćta");

        // wybierajcie -> wybierajta
        message = RegexAjcie.Replace(message, "ajta");

        // Bierzcie -> bierzta
        message = RegexRzcie.Replace(message, "rzta");

        // Powiedzioł, powiedzioła, powiedzioło, powiedziołu
        message = RegexAl.Replace(message, "oł");
        message = RegexAla.Replace(message, "oła");
        message = RegexAlo.Replace(message, "oło"); // neutratyw
        message = RegexAlu.Replace(message, "ołu"); // dukaizm

        // Powiedziołeś, powiedziołaś, powiedzołoś, powiedziołuś
        message = RegexAles.Replace(message, "ołeś");
        message = RegexAlas.Replace(message, "ołaś");
        message = RegexAlos.Replace(message, "ołoś"); // neutratyw
        message = RegexAlus.Replace(message, "ołuś"); // dukaizm

        // Powiedziołem, powiedziołam, powiedziołom, powiedziołum
        message = RegexAlem.Replace(message, "ołem");
        message = RegexAlam.Replace(message, "ołam");
        message = RegexAlom.Replace(message, "ołom"); // neutratyw
        message = RegexAlum.Replace(message, "ołum"); // dukaizm

        args.Message = message;
    }
}
