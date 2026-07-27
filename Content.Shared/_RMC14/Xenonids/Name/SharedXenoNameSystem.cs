using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.NameModifier.EntitySystems;
using Robust.Shared.Network;

namespace Content.Shared._RMC14.Xenonids.Name;

public abstract partial class SharedXenoNameSystem : EntitySystem
{
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private NameModifierSystem _nameModifier = default!;
    [Dependency] private INetManager _net = default!;

    private const string DefaultPrefix = "XX";

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoNameComponent, NewXenoEvolvedEvent>(OnNewXenoEvolved);
        SubscribeLocalEvent<XenoNameComponent, XenoDevolvedEvent>(OnXenoDevolved);

        SubscribeLocalEvent<XenoNameComponent, RefreshNameModifiersEvent>(OnRefreshNameModifiers);
        SubscribeLocalEvent<XenoNameComponent, MindAddedMessage>(OnMindAdded);
    }

    private void OnNewXenoEvolved(Entity<XenoNameComponent> ent, ref NewXenoEvolvedEvent ev)
    {
        TransferName(ev.OldXeno, ent.Owner);
    }

    private void OnXenoDevolved(Entity<XenoNameComponent> ent, ref XenoDevolvedEvent ev)
    {
        TransferName(ev.OldXeno, ent.Owner);
    }

    private void OnRefreshNameModifiers(Entity<XenoNameComponent> ent, ref RefreshNameModifiersEvent args)
    {
        var rank = ent.Comp.Rank;
        if (rank.Length > 0)
            rank = $"{rank} ";

        var prefix = ent.Comp.Prefix;
        if (prefix.Length == 0)
            prefix = DefaultPrefix;

        var postfix = ent.Comp.Postfix;
        var number = ent.Comp.Number;

        if (HasComp<XenoOmitNumberComponent>(ent))
        {
            args.AddModifier("rmc-xeno-name", extraArgs: [("rank", rank), ("prefix", prefix), ("postfix", postfix)]);
        }
        else
        {
            if (postfix.Length > 0)
                postfix = $"-{postfix}";

            args.AddModifier("rmc-xeno-name-number", extraArgs: [("rank", rank), ("prefix", prefix), ("number", number), ("postfix", postfix)]);
        }

        if (_mind.TryGetMind(ent, out _, out var mind))
            mind.CharacterName = args.GetModifiedName();
    }

    private void OnMindAdded(Entity<XenoNameComponent> ent, ref MindAddedMessage args)
    {
        SetupName(ent);
    }

    private void TransferName(EntityUid oldXeno, EntityUid newXeno)
    {
        if (_net.IsClient)
            return;

        if (!TryComp(oldXeno, out XenoNameComponent? oldName))
            return;

        var newName = EnsureComp<XenoNameComponent>(newXeno);
        newName.Rank = oldName.Rank;
        newName.Prefix = oldName.Prefix;
        newName.Number = oldName.Number;
        newName.Postfix = oldName.Postfix;
        Dirty(newXeno, newName);
        RemComp<AssignXenoNameComponent>(newXeno);

        _nameModifier.RefreshNameModifiers(newXeno);
    }

    public virtual void SetupName(EntityUid xeno)
    {
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<AssignXenoNameComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            SetupName(uid);
        }
    }
}
