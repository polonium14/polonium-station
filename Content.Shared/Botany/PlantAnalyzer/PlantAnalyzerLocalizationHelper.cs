// SPDX-FileCopyrightText: 2025 Mehnix <56132549+Mehnix@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Atmos;
using Content.Shared.Atmos.Prototypes;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Localizations;
using Robust.Shared.Prototypes;

namespace Content.Shared.Botany.PlantAnalyzer;
public sealed class PlantAnalyzerLocalizationHelper
{
    public const string DP = "0.00"; //number of decimal points to use in toString() printing
    public static string GasesToLocalizedStrings(List<Gas> gases, IPrototypeManager protMan)
    {
        if (gases.Count == 0)
            return "";

        List<int> gasIds = [];
        foreach (var gas in gases)
            gasIds.Add((int)gas);

        List<string> gasesLoc = [];
        foreach (var gas in protMan.EnumeratePrototypes<GasPrototype>())
            if (gasIds.Contains(int.Parse(gas.ID)))
                gasesLoc.Add(Loc.GetString(gas.Name));

        return ContentLocalizationManager.FormatList(gasesLoc);
    }

    public static string ChemicalsToLocalizedStrings(List<string> ids, IPrototypeManager protMan)
    {
        if (ids.Count == 0)
            return "";

        List<string> locStrings = [];
        foreach (var id in ids)
            locStrings.Add(protMan.TryIndex<ReagentPrototype>(id, out var prototype) ? prototype.LocalizedName : id);

        return ContentLocalizationManager.FormatList(locStrings);
    }

    public static (string Singular, string Plural) ProduceToLocalizedStrings(List<string> ids, IPrototypeManager protMan)
    {
        if (ids.Count == 0)
            return ("", "");

        List<string> singularStrings = [];
        List<string> pluralStrings = [];
        foreach (var id in ids)
        {
            var singular = protMan.TryIndex<EntityPrototype>(id, out var prototype) ? prototype.Name : id;
            var plural = Loc.GetString("plant-analyzer-produce-plural", ("thing", singular));

            singularStrings.Add(singular);
            pluralStrings.Add(plural);
        }

        return (
            ContentLocalizationManager.FormatListToOr(singularStrings),
            ContentLocalizationManager.FormatListToOr(pluralStrings)
        );
    }

    public static string BooleanToLocalizedStrings(bool choice, IPrototypeManager protMan)
    {
        if (choice == true)
            return Loc.GetString("plant-analyzer-produce-yes");
        else
            return Loc.GetString("plant-analyzer-produce-no");
    }
}
