using System.Text.RegularExpressions;
using Content.Shared.Speech.Components;
using Content.Shared.Speech.EntitySystems;
using Robust.Shared.GameStates;
using Robust.Shared.Random;

namespace Content.Shared._Polonium.Body;

/// <summary>
/// Polish lisp from a damaged tongue: the hushing sounds come out hissing.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(PolishLispSystem), Other = AccessPermissions.ReadExecute)]
public sealed partial class PolishLispComponent : BaseAccentComponent
{
    /// <summary>
    /// Chance that each hushing sound is lisped, 0 to 1.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Chance = 1f;
}

public sealed partial class PolishLispSystem : RelayAccentSystem<PolishLispComponent>
{
    [Dependency] private IRobustRandom _random = default!;

    // sz → s, cz → c, dż → dz, rz and ż → z. "dż" before "ż", so the d stays.
    private static readonly Regex Hushing = new("[Ss][Zz]|[Cc][Zz]|[Dd][Żż]|[Rr][Zz]|[Żż]", RegexOptions.Compiled);

    public override string Accentuate(string message, Entity<PolishLispComponent>? ent = null)
    {
        var chance = ent?.Comp.Chance ?? 1f;
        return Hushing.Replace(message, match => _random.Prob(chance) ? Lisp(match.Value) : match.Value);
    }

    /// <summary>
    /// Sets how strongly the entity lisps, removing the lisp at 0.
    /// </summary>
    public void SetChance(EntityUid uid, float chance)
    {
        if (chance <= 0.01f)
        {
            RemComp<PolishLispComponent>(uid);
            return;
        }

        var lisp = EnsureComp<PolishLispComponent>(uid);
        if (MathF.Abs(lisp.Chance - chance) < 0.01f)
            return;

        lisp.Chance = chance;
        Dirty(uid, lisp);
    }

    private static string Lisp(string sound)
    {
        var upper = char.IsUpper(sound[0]);
        var whole = sound.Length > 1 && char.IsUpper(sound[1]);
        var lisped = char.ToLowerInvariant(sound[0]) switch
        {
            's' => "s",
            'c' => "c",
            'd' => "dz",
            _ => "z", // rz, ż
        };

        if (whole)
            return lisped.ToUpperInvariant();

        if (!upper)
            return lisped;

        // Spelled out: joining a char to a string compiles to a span constructor the client sandbox forbids.
        return lisped switch
        {
            "s" => "S",
            "c" => "C",
            "dz" => "Dz",
            _ => "Z",
        };
    }
}
