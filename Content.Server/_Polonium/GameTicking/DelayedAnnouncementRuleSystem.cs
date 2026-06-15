using Content.Server.Chat.Systems;
using Content.Server.GameTicking.Rules;
using Content.Shared._Polonium.GameTicking;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;

namespace Content.Server._Polonium.GameTicking;

public sealed class DelayedAnnouncementRuleSystem : GameRuleSystem<DelayedAnnouncementRuleComponent>
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    protected override void Started(
        EntityUid uid,
        DelayedAnnouncementRuleComponent comp,
        GameRuleComponent gameRule,
        GameRuleStartedEvent args)
    {
        base.Started(uid, comp, gameRule, args);

        var filter = Filter.Empty().AddWhere(GameTicker.UserHasJoinedGame);
        var sender = comp.Sender != null ? Loc.GetString(comp.Sender) : null;

        _chat.DispatchFilteredAnnouncement(
            filter,
            Loc.GetString(comp.Announcement),
            sender: sender,
            playSound: comp.Sound == null,
            colorOverride: comp.Color);

        if (comp.Sound != null)
            _audio.PlayGlobal(comp.Sound, filter, true);

        GameTicker.EndGameRule(uid, gameRule);
    }
}
