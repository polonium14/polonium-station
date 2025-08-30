using System.Text.RegularExpressions;
using Content.Server.Speech.Components;
using Robust.Shared.Random;

namespace Content.Server.Speech.EntitySystems;

public sealed class KurwalskiAccentSystem : EntitySystem
{
    private static readonly Regex RegexPeriod = new(@"\.");
    private static readonly Regex RegexComma = new(@"\,");
    private static readonly Regex RegexExclaim = new(@"\!");
    private static readonly Regex RegexQuestion = new(@"\?");

    private static readonly String[] KurwyPoczatek = {
        "Kurwa ",
        "Nosz kurwa, ",
        "Ty chuju, ",
        "No japierdole, ",
        "Japierdole, "
    };

    private static readonly String[] KurwyKoniec = {
        " kurwa",
        " kurwo jebana.",
        " chuju.",
        " pojebie.",
        " debilu jebany."
    };

    [Dependency] private readonly IRobustRandom _random = default!;


    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<KurwalskiAccentComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(EntityUid uid, KurwalskiAccentComponent component, AccentGetEvent args)
    {
        var message = args.Message;


        message = RegexPeriod.Replace(message, ". kurwa.");
        message = RegexComma.Replace(message, ", kurwa");
        message = RegexExclaim.Replace(message, " kurwa!");
        message = RegexQuestion.Replace(message, " kurwa?");


        if (!_random.Prob(0.4f))
        {
            args.Message = message;
            return;
        }

        switch (_random.Next(1, 3))
        {

            case 1:
                message = KurwyPoczatek[_random.Next(0, KurwyPoczatek.Length)] + message;
                break;

            case 2:
                message = message + KurwyKoniec[_random.Next(0, KurwyKoniec.Length)];
                break;
        }

        args.Message = message;
    }
}

// Miałem za dużo fajdy ztym... - Tofi_Dev