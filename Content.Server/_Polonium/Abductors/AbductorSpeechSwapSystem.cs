using Content.Shared.Administration.Logs;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.Popups;
using Content.Shared.Speech.EntitySystems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.Abductors;

public sealed partial class AbductorSpeechSwapSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AbductorSpeechSwapComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<AbductorSpeechSwapComponent, TransformSpeechEvent>(OnTransformSpeech, before: [typeof(AccentSystem)]);
    }

    private void OnStartup(Entity<AbductorSpeechSwapComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.NextRoll = _timing.CurTime + NextDelay(ent.Comp);
    }

    private void OnTransformSpeech(Entity<AbductorSpeechSwapComponent> ent, ref TransformSpeechEvent args)
    {
        var now = _timing.CurTime;
        if (!args.Typed || now < ent.Comp.NextRoll)
            return;

        ent.Comp.NextRoll = now + NextDelay(ent.Comp);
        if (!_random.Prob(ent.Comp.Chance))
            return;

        var original = args.Message;
        var line = Loc.GetString(_random.Pick(ProtoMan.Index(ent.Comp.Lines).Values), ("speaker", ent.Owner));
        args.Message = line;

        _adminLog.Add(LogType.Chat, LogImpact.Low, $"An abductor gland replaced what {ToPrettyString(ent):player} tried to say, \"{original}\", with \"{line}\".");
        _popup.PopupEntity(Loc.GetString(ent.Comp.Popup), ent, ent, PopupType.MediumCaution);
    }

    private TimeSpan NextDelay(AbductorSpeechSwapComponent comp)
    {
        return _random.Next(comp.MinInterval, comp.MaxInterval);
    }
}
