using Content.Shared._Polonium.Chat;
using Content.Shared._Polonium.Horror;
using Content.Shared._Polonium.Horror.Components;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.Ghost.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._Polonium.Horror;

/// <summary>
/// The silent talk inside one head: the horror and its host, and while the horror drives the body, the trapped mind.
/// Ghosts hear it, like in Aquila.
/// </summary>
public sealed partial class EldritchHorrorSystem
{
    private static readonly EntProtoId TalkToHorrorAction = "ActionHorrorTalkToHorror";

    private void InitializeSpeech()
    {
        SubscribeLocalEvent<EldritchHorrorComponent, PoloniumSpeechRedirectEvent>(OnHorrorSpeech);
        SubscribeLocalEvent<EldritchHorrorInfestedComponent, PoloniumSpeechRedirectEvent>(OnHostSpeech);
        SubscribeLocalEvent<HorrorCaptiveMindComponent, PoloniumSpeechRedirectEvent>(OnCaptiveSpeech);
        SubscribeLocalEvent<EldritchHorrorInfestedComponent, EntitySpokeEvent>(OnHostSpoke);

        SubscribeLocalEvent<EldritchHorrorComponent, HorrorMindLinkActionEvent>(OnHorrorMindLink);
        SubscribeLocalEvent<EldritchHorrorInfestedComponent, HorrorMindLinkActionEvent>(OnHostMindLink);
        SubscribeLocalEvent<EldritchHorrorInfestedComponent, ComponentStartup>(OnInfestedStartup);
        SubscribeLocalEvent<EldritchHorrorInfestedComponent, ComponentShutdown>(OnInfestedShutdown);
    }

    private void OnInfestedStartup(Entity<EldritchHorrorInfestedComponent> ent, ref ComponentStartup args)
    {
        _actions.AddAction(ent, ref ent.Comp.TalkToHorrorAction, TalkToHorrorAction);
    }

    private void OnInfestedShutdown(Entity<EldritchHorrorInfestedComponent> ent, ref ComponentShutdown args)
    {
        if (!TerminatingOrDeleted(ent))
            _actions.RemoveAction(ent.Owner, ent.Comp.TalkToHorrorAction);
    }

    /// <summary>
    /// A horror sitting in a head can't talk out loud, everything it says goes to the host.
    /// </summary>
    private void OnHorrorSpeech(Entity<EldritchHorrorComponent> ent, ref PoloniumSpeechRedirectEvent args)
    {
        if (args.Handled || ent.Comp.Controlling || ent.Comp.Host is not { } host)
            return;

        args.Handled = true;
        SendLinkMessage(ent, [ent.Owner, host], args.Message);
    }

    private void OnHostSpeech(Entity<EldritchHorrorInfestedComponent> ent, ref PoloniumSpeechRedirectEvent args)
    {
        if (args.Handled || ent.Comp.Horror is not { } horror || !TryComp<EldritchHorrorComponent>(horror, out var comp))
            return;

        if (comp.Controlling)
        {
            // the horror in the body talks to the mind it locked away
            if (!comp.TalkToCaptive || ent.Comp.CaptiveMindContainer.ContainedEntity is not { } captive)
                return;

            args.Handled = true;
            SendLinkMessage(ent, [ent.Owner, captive], args.Message);
            return;
        }

        if (!ent.Comp.TalkToHorror)
            return;

        args.Handled = true;
        SendLinkMessage(ent, [ent.Owner, horror], args.Message);
    }

    private void OnCaptiveSpeech(Entity<HorrorCaptiveMindComponent> ent, ref PoloniumSpeechRedirectEvent args)
    {
        args.Handled = true;
        if (ent.Comp.Host is { } host)
            SendLinkMessage(ent, [ent.Owner, host], args.Message);
    }

    private void OnHostSpoke(Entity<EldritchHorrorInfestedComponent> ent, ref EntitySpokeEvent args)
    {
        ent.Comp.RecentSpeech.Add(args.Message);
        if (ent.Comp.RecentSpeech.Count > ent.Comp.RecentSpeechLimit)
            ent.Comp.RecentSpeech.RemoveAt(0);
    }

    private void OnHorrorMindLink(Entity<EldritchHorrorComponent> ent, ref HorrorMindLinkActionEvent args)
    {
        if (args.Handled || !ent.Comp.Controlling)
            return;

        ent.Comp.TalkToCaptive = !ent.Comp.TalkToCaptive;
        Dirty(ent);
        _actions.SetToggled(args.Action.AsNullable(), ent.Comp.TalkToCaptive);
        _popup.PopupEntity(Loc.GetString(ent.Comp.TalkToCaptive ? "horror-link-captive-on" : "horror-link-captive-off"), args.Performer, args.Performer);
        args.Handled = true;
    }

    private void OnHostMindLink(Entity<EldritchHorrorInfestedComponent> ent, ref HorrorMindLinkActionEvent args)
    {
        if (args.Handled)
            return;

        ent.Comp.TalkToHorror = !ent.Comp.TalkToHorror;
        Dirty(ent);
        _actions.SetToggled(args.Action.AsNullable(), ent.Comp.TalkToHorror);
        _popup.PopupEntity(Loc.GetString(ent.Comp.TalkToHorror ? "horror-link-host-on" : "horror-link-host-off"), ent, ent);
        args.Handled = true;
    }

    /// <summary>
    /// Sends a silent message to everyone in the link and to all ghosts.
    /// </summary>
    private void SendLinkMessage(EntityUid speaker, List<EntityUid> listeners, string message)
    {
        var wrapped = Loc.GetString("horror-link-message",
            ("name", FormattedMessage.EscapeText(Name(speaker))),
            ("message", FormattedMessage.EscapeText(message)));

        var filter = Filter.Empty();
        foreach (var listener in listeners)
        {
            if (TryComp<ActorComponent>(listener, out var actor))
                filter.AddPlayer(actor.PlayerSession);
        }

        filter.AddWhereAttachedEntity(HasComp<GhostComponent>);
        _chat.ChatMessageToManyFiltered(filter, ChatChannel.Local, message, wrapped, speaker, false, true, null);
        _adminLog.Add(LogType.Chat, LogImpact.Low, $"Horror link from {ToPrettyString(speaker):user}: {message}");
    }

    /// <summary>
    /// A chat message only the horror's player sees.
    /// </summary>
    private void SendPrivate(EntityUid horror, string message)
    {
        if (!TryComp<ActorComponent>(horror, out var actor))
            return;

        var wrapped = Loc.GetString("horror-private-message", ("message", message));
        _chat.ChatMessageToOne(ChatChannel.Local, message, wrapped, horror, false, actor.PlayerSession.Channel);
    }
}
