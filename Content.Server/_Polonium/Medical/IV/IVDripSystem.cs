using System.Diagnostics.CodeAnalysis;
using Content.Server.Chat.Systems;
using Content.Shared.Body.Components;
using Content.Shared._Polonium.Medical.IV;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.Medical.IV;

public sealed partial class IVDripSystem : SharedIVDripSystem
{
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainer = default!;

    private bool TryGetBloodstream(
        EntityUid attachedTo,
        [NotNullWhen(true)] out Entity<SolutionComponent>? solEnt,
        [NotNullWhen(true)] out Solution? solution)
    {
        solEnt = default;
        solution = default;
        return TryComp(attachedTo, out BloodstreamComponent? attachedStream)
            && _solutionContainer.TryGetSolution(attachedTo, attachedStream.BloodSolutionName, out solEnt, out solution);
    }

    protected override void DoRip(DamageSpecifier? damage, EntityUid attached, EntityUid? user, ProtoId<EmotePrototype> ripEmote)
    {
        base.DoRip(damage, attached, user, ripEmote);
        _chat.TryEmoteWithoutChat(attached, ripEmote);
    }

    /// <summary>
    /// Moves one tick's worth of reagents between <paramref name="pack"/> and the bloodstream of
    /// <paramref name="attachedTo"/>. Shared by the drip stand and the strapped-on bag, which only
    /// differ in where the pack and the transfer amount come from.
    /// </summary>
    private void TransferReagents(Entity<IVBagComponent> pack, EntityUid attachedTo, bool injecting, FixedPoint2 transferAmount)
    {
        if (!_solutionContainer.TryGetSolution(pack.Owner, pack.Comp.Solution, out var packSolEnt, out var packSol))
            return;

        if (!TryGetBloodstream(attachedTo, out var streamSolEnt, out var streamSol))
            return;

        if (injecting)
        {
            var taken = _solutionContainer.SplitSolution(packSolEnt.Value, transferAmount);
            // whatever is not a transferable reagent rides along separately
            var chems = taken.SplitSolutionWithout(taken.Volume, pack.Comp.TransferableReagents);

            AddOrPutBack(streamSolEnt.Value, packSolEnt.Value, streamSol, taken);
            AddOrPutBack(streamSolEnt.Value, packSolEnt.Value, streamSol, chems);

            Dirty(packSolEnt.Value);
        }
        else if (packSol.Volume < packSol.MaxVolume)
        {
            _solutionContainer.TryTransferSolution(packSolEnt.Value, streamSol, transferAmount);
            Dirty(streamSolEnt.Value);
        }
    }

    /// <summary>
    /// Adds <paramref name="portion"/> to the bloodstream if it fits, otherwise puts it back in the pack.
    /// </summary>
    private void AddOrPutBack(Entity<SolutionComponent> stream,
        Entity<SolutionComponent> pack,
        Solution streamSol,
        Solution portion)
    {
        if (portion.Volume <= 0)
            return;

        _solutionContainer.TryAddSolution(streamSol.AvailableVolume >= portion.Volume ? stream : pack, portion);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var time = _timing.CurTime;

        var ivs = EntityQueryEnumerator<IVDripComponent>();
        while (ivs.MoveNext(out var ivId, out var ivComp))
        {
            if (ivComp.AttachedTo is not { } attachedTo)
                continue;

            if (!InRange(ivId, attachedTo, ivComp.Range))
            {
                DetachIV((ivId, ivComp), null, true);
                continue;
            }

            if (time < ivComp.TransferAt)
                continue;

            if (_itemSlots.GetItemOrNull(ivId, ivComp.Slot) is not { } pack ||
                !TryComp(pack, out IVBagComponent? packComp))
            {
                continue;
            }

            ivComp.TransferAt = time + ivComp.TransferDelay;

            TransferReagents((pack, packComp), attachedTo, ivComp.Injecting, ivComp.TransferAmount);

            Dirty(ivId, ivComp);
            UpdateIVVisuals((ivId, ivComp));
            UpdatePackVisuals((pack, packComp));
        }

        var packs = EntityQueryEnumerator<IVBagComponent>();
        while (packs.MoveNext(out var packId, out var packComp))
        {
            if (packComp.AttachedTo is not { } attachedTo)
                continue;

            if (!InRange(packId, attachedTo, packComp.Range))
            {
                DetachPack((packId, packComp), null, true);
                continue;
            }

            if (time < packComp.TransferAt)
                continue;

            packComp.TransferAt = time + packComp.TransferDelay;

            TransferReagents((packId, packComp), attachedTo, packComp.Injecting, packComp.TransferAmount);

            Dirty(packId, packComp);
            UpdatePackVisuals((packId, packComp));
        }
    }
}
