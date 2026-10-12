using Content.Server.Chat.Systems;
using Content.Shared._Polonium.Polish;
using Content.Shared.Chat;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.Polish;

/// <summary>
/// Machanie sztandarem: okrzyk, leczenie i skrócenie ogłuszenia posiadacza.
/// Sztandar z NSV13 inspirował też sojuszników z listy zawodów, ale polski sztandar takiej listy nie miał,
/// więc działał tylko na posiadacza - tak samo jest tutaj.
/// </summary>
public sealed partial class InspiringBannerSystem : EntitySystem
{
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private StatusEffectsSystem _status = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InspiringBannerComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<InspiringBannerComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<InspiringBannerComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("inspiring-banner-examine"));
    }

    private void OnUseInHand(Entity<InspiringBannerComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var user = args.User;

        if (_timing.CurTime < ent.Comp.NextUse)
        {
            _popup.PopupEntity(Loc.GetString("inspiring-banner-not-ready", ("banner", ent.Owner)), user, user);
            return;
        }

        ent.Comp.NextUse = _timing.CurTime + ent.Comp.Cooldown;
        Dirty(ent);

        _popup.PopupEntity(
            Loc.GetString("inspiring-banner-flourish-self", ("banner", ent.Owner)),
            Loc.GetString("inspiring-banner-flourish-others", ("user", user), ("banner", ent.Owner)),
            user,
            user,
            PopupType.Medium);
        _audio.PlayPvs(ent.Comp.FlourishSound, ent);

        if (ent.Comp.Warcry is { } warcry)
            _chat.TrySendInGameICMessage(user, Loc.GetString(warcry), InGameICChatType.Speak, false, ignoreActionBlocker: true);

        foreach (var (group, amount) in ent.Comp.Heal)
        {
            _damageable.HealEvenly(user, -amount, group);
        }
        _status.TryRemoveTime(user, SharedStunSystem.StunId, ent.Comp.StunReduction);
        _stun.AddKnockdownTime(user, -ent.Comp.StunReduction);
        _audio.PlayPvs(ent.Comp.HealSound, user);
    }
}
