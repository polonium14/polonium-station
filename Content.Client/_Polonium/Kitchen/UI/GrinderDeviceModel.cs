using Content.Shared._Polonium.Kitchen;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Kitchen;
using Content.Shared.Kitchen.Components;
using Content.Shared.Kitchen.EntitySystems;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Stacks;
using Content.Shared.Whitelist;
using Robust.Client.Player;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Polonium.Kitchen.UI;

/// <param name="Amount">Units of the reagent.</param>
public readonly record struct GrinderGrain(Color Color, float Amount);

/// <summary>
/// One thing in the chamber that the running program will turn into reagents.
/// </summary>
public sealed class GrinderPlanEntry
{
    public EntityUid Item;
    public float Volume;
    public float DryVolume;
    public Color Color;

    /// <summary>
    /// Whether the whole thing is used up. A stack that does not fit into the vessel loses only a part.
    /// </summary>
    public bool All;

    public readonly List<GrinderGrain> Grains = new();

    /// <summary>
    /// The part of the run in which the blades take this thing, from 0 to 1.
    /// </summary>
    public float From;

    public float To;
    public float Spray;
    public bool Settled;
}

/// <summary>
/// Reads the state of one reagent grinder for its window.
/// </summary>
public sealed class GrinderDeviceModel
{
    private static readonly LocId StackName = "polonium-grinder-note-stack";

    private readonly EntityUid _owner;
    private readonly IEntityManager _entMan;
    private readonly IPrototypeManager _proto;
    private readonly IGameTiming _timing;
    private readonly IPlayerManager _player;
    private readonly SharedReagentGrinderSystem _grinder;
    private readonly SharedSolutionContainerSystem _solutions;
    private readonly SharedPowerReceiverSystem _power;
    private readonly SharedHandsSystem _hands;
    private readonly ItemSlotsSystem _slots;
    private readonly EntityWhitelistSystem _whitelist;
    private readonly List<EntityUid> _contents = new();

    /// <summary>
    /// What is in the chamber, in the order it came in. A thing the client only guesses to be ground up stays listed
    /// until the server confirms it.
    /// </summary>
    public IReadOnlyList<EntityUid> Contents => _contents;

    /// <summary>
    /// Whether the client is still waiting for the server to say what a finished run ground up.
    /// Until then the client's guess changes from tick to tick, so the contents shown should not be trusted.
    /// </summary>
    public bool Settling { get; private set; }

    public int Capacity { get; private set; }
    public EntityUid? Vessel { get; private set; }
    public Solution? VesselSolution { get; private set; }

    /// <summary>
    /// The thing in the player's hand, when it fits into the empty vessel slot.
    /// </summary>
    public EntityUid? HandVessel { get; private set; }

    public bool Powered { get; private set; }

    /// <summary>
    /// The running program. Null while the grinder stands still.
    /// </summary>
    public GrinderProgram? Program { get; private set; }

    /// <summary>
    /// How much of the run is over, from 0 to 1.
    /// </summary>
    public float Progress { get; private set; }

    /// <summary>
    /// Whether the grinder runs or the client still waits to hear what its last run made. Nothing can be started or taken out then.
    /// </summary>
    public bool Busy => Program != null || Settling;

    public GrinderAutoMode AutoMode { get; private set; }
    public bool CanGrind { get; private set; }
    public bool CanJuice { get; private set; }

    public GrinderDeviceModel(EntityUid owner, IEntityManager entMan, IPrototypeManager proto, IGameTiming timing, IPlayerManager player)
    {
        _owner = owner;
        _entMan = entMan;
        _proto = proto;
        _timing = timing;
        _player = player;
        _grinder = entMan.System<SharedReagentGrinderSystem>();
        _solutions = entMan.System<SharedSolutionContainerSystem>();
        _power = entMan.System<SharedPowerReceiverSystem>();
        _hands = entMan.System<SharedHandsSystem>();
        _slots = entMan.System<ItemSlotsSystem>();
        _whitelist = entMan.System<EntityWhitelistSystem>();
    }

    /// <summary>
    /// Reads the grinder again. Called once a frame.
    /// </summary>
    public void Refresh()
    {
        Settling = false;
        Vessel = null;
        VesselSolution = null;
        HandVessel = null;
        Program = null;
        Progress = 0f;
        Powered = false;
        CanGrind = false;
        CanJuice = false;

        if (!_entMan.TryGetComponent(_owner, out ReagentGrinderComponent? grinder))
        {
            _contents.Clear();
            return;
        }

        Capacity = grinder.StorageMaxEntities;
        AutoMode = grinder.AutoMode;
        Powered = _power.IsPowered(_owner);
        ReadContents((Container?) grinder.InputContainer);

        if (grinder is { EndTime: { } end, Program: { } program })
        {
            Program = program;
            var length = grinder.WorkTime * grinder.WorkTimeMultiplier;
            Progress = length > TimeSpan.Zero
                ? Math.Clamp(1f - (float) ((end - _timing.CurTime) / length), 0f, 1f)
                : 1f;
        }

        if (_contents.Count > 0)
        {
            CanGrind = true;
            CanJuice = true;
            foreach (var item in _contents)
            {
                CanGrind &= _grinder.CanGrind(item);
                CanJuice &= _grinder.CanJuice(item);
            }
        }

        Vessel = _slots.GetItemOrNull(_owner, ReagentGrinderComponent.BeakerSlotId);
        if (Vessel is { } vessel)
        {
            if (_solutions.TryGetFitsInDispenser(vessel, out _, out var solution))
                VesselSolution = solution;

            return;
        }

        if (_player.LocalEntity is { } user
            && _hands.GetActiveItem(user) is { } held
            && _slots.TryGetSlot(_owner, ReagentGrinderComponent.BeakerSlotId, out var slot)
            && !slot.Locked
            && _whitelist.CheckBoth(held, slot.Blacklist, slot.Whitelist))
        {
            HandVessel = held;
        }
    }

    /// <summary>
    /// The client ends a run a few ticks before the server and predicts the deletion of what was ground up by taking
    /// it out of the container. Every tick it guesses another thing, so a thing that is only predicted to be deleted
    /// keeps its place in the list.
    /// </summary>
    private void ReadContents(Container? input)
    {
        IReadOnlyList<EntityUid> listed = input?.ContainedEntities ?? Array.Empty<EntityUid>();
        for (var i = _contents.Count - 1; i >= 0; i--)
        {
            var uid = _contents[i];
            if (Lists(listed, uid))
                continue;

            if (!_entMan.Deleted(uid) && _entMan.IsQueuedForDeletion(uid))
                Settling = true;
            else
                _contents.RemoveAt(i);
        }

        for (var i = 0; i < listed.Count; i++)
        {
            if (!_contents.Contains(listed[i]))
                _contents.Add(listed[i]);
        }
    }

    /// <summary>
    /// Whether a listed thing has left the chamber on the client's guess and only waits for the server to delete it.
    /// </summary>
    public bool Awaited(EntityUid uid)
    {
        return _entMan.IsQueuedForDeletion(uid);
    }

    private static bool Lists(IReadOnlyList<EntityUid> list, EntityUid uid)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i] == uid)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Works out what the program will take from the chamber, by the same arithmetic the game uses when the run ends.
    /// Things that do not fit into the vessel are left out.
    /// </summary>
    public void BuildPlan(GrinderProgram program, List<GrinderPlanEntry> plan)
    {
        plan.Clear();
        if (VesselSolution is not { } vessel)
            return;

        _entMan.TryGetComponent(_owner, out GrinderMassVisualsComponent? visuals);
        var available = vessel.AvailableVolume;
        foreach (var item in _contents)
        {
            if (Awaited(item) || _grinder.GetGrinderSolution(item, program) is not { } solution)
                continue;

            var count = 1;
            var all = true;
            if (_entMan.TryGetComponent(item, out StackComponent? stack))
            {
                var total = solution.Volume * stack.Count;
                if (total <= 0)
                    continue;

                count = (int) (stack.Count * FixedPoint2.Min(available / total + 0.01, 1));
                if (count <= 0)
                    continue;

                all = count == stack.Count;
            }
            else if (solution.Volume > available)
            {
                continue;
            }

            var entry = new GrinderPlanEntry
            {
                Item = item,
                Volume = solution.Volume.Float() * count,
                Color = solution.GetColor(_proto),
                All = all,
            };

            foreach (var (reagent, quantity) in solution.Contents)
            {
                var amount = quantity.Float() * count;
                var color = Color.Gray;
                var dry = visuals != null && visuals.DryReagents.Contains(reagent.Prototype);
                if (_proto.TryIndex<ReagentPrototype>(reagent.Prototype, out var prototype))
                {
                    color = prototype.SubstanceColor;
                    dry |= visuals != null && prototype.MeltingPoint > visuals.DryMeltingPoint;
                }

                entry.Grains.Add(new GrinderGrain(color, amount));
                if (dry)
                    entry.DryVolume += amount;
            }

            // The game uses the thing up even when the rounding of a stack leaves no room for what it gives.
            var volume = solution.Volume * count;
            if (volume <= available)
                available -= volume;

            plan.Add(entry);
        }
    }

    /// <summary>
    /// The share of dry reagents from which the mass is drawn as dust.
    /// </summary>
    public float DustShare => _entMan.TryGetComponent(_owner, out GrinderMassVisualsComponent? visuals) ? visuals.DustShare : float.PositiveInfinity;

    /// <summary>
    /// The name of a thing in the chamber or of the vessel, as the note writes it.
    /// </summary>
    public string Name(EntityUid uid, out int count)
    {
        count = _entMan.TryGetComponent(uid, out StackComponent? stack) ? stack.Count : 0;
        return _entMan.TryGetComponent(uid, out MetaDataComponent? meta) ? meta.EntityName : string.Empty;
    }

    /// <summary>
    /// The row of the note for a thing in the chamber: its name and, for a stack, how many are left.
    /// </summary>
    public static string Row(string name, int count)
    {
        return count > 0 ? Loc.GetString(StackName, ("name", name), ("count", count)) : name;
    }
}
