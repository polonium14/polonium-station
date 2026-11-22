using Content.Shared.Paper;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;
using static Content.Shared.Paper.PaperComponent;

namespace Content.Server._Polonium.Paper;

/// <summary>
/// Any client with a paper window open can send text, so the server tracks who
/// opened the paper with a writing tool and rejects everyone else.
/// </summary>
public sealed partial class PaperWriteGuardSystem : EntitySystem
{
    [Dependency] private PaperSystem _paper = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private TagSystem _tag = default!;

    private static readonly ProtoId<TagPrototype> IgnoreStampsTag = "WriteIgnoreStamps";
    private static readonly LocId NoToolMessage = "paper-write-no-tool";
    private static readonly LocId StampedMessage = "paper-write-stamped";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TagComponent, PaperWriteEvent>(OnWriteStarted);
        SubscribeLocalEvent<PaperComponent, BoundUserInterfaceMessageAttempt>(OnMessageAttempt);
        Subs.BuiEvents<PaperComponent>(PaperUiKey.Key, subs => subs.Event<BoundUIClosedEvent>(OnUiClosed));
    }

    /// <summary>
    /// Lets the user save text on the paper until someone saves or the user closes its window.
    /// </summary>
    public void AllowWriting(EntityUid paper, EntityUid user, bool ignoreStamps = false)
    {
        EnsureComp<PaperWritersComponent>(paper).Writers[user] = ignoreStamps;
    }

    private void OnWriteStarted(Entity<TagComponent> ent, ref PaperWriteEvent args)
    {
        AllowWriting(args.Paper, args.User, _tag.HasTag(ent.Owner, IgnoreStampsTag));
    }

    private void OnUiClosed(Entity<PaperComponent> ent, ref BoundUIClosedEvent args)
    {
        if (TryComp<PaperWritersComponent>(ent, out var writers))
            writers.Writers.Remove(args.Actor);
    }

    private void OnMessageAttempt(Entity<PaperComponent> ent, ref BoundUserInterfaceMessageAttempt args)
    {
        if (args.Cancelled || args.Message is not PaperInputTextMessage message)
            return;

        if (message.Text == null)
        {
            args.Cancel();
            return;
        }

        if (!TryComp<PaperWritersComponent>(ent, out var writers) ||
            !writers.Writers.TryGetValue(args.Actor, out var ignoreStamps))
        {
            Reject(ent, ref args, NoToolMessage);
            return;
        }

        if (ent.Comp.StampedBy.Count > 0 && !ignoreStamps)
        {
            writers.Writers.Remove(args.Actor);
            ent.Comp.Mode = PaperAction.Read;
            Reject(ent, ref args, StampedMessage);
            return;
        }

        // A save switches the paper back to reading for everyone who has it open.
        writers.Writers.Clear();
    }

    private void Reject(Entity<PaperComponent> ent, ref BoundUserInterfaceMessageAttempt args, LocId message)
    {
        args.Cancel();
        _popup.PopupCursor(Loc.GetString(message), args.Actor, PopupType.SmallCaution);

        // The client empties its editor right after saving, so send the text back.
        _paper.UpdateUserInterface(ent);
    }
}
