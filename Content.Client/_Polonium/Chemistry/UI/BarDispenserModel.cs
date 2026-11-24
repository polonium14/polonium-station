using Content.Shared._Polonium.Chemistry;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Storage;
using Content.Shared.Whitelist;
using Robust.Client.Player;
using Robust.Shared.Prototypes;

namespace Content.Client._Polonium.Chemistry.UI;

/// <summary>
/// What a poured drink looks like on its way from the nozzle to the vessel.
/// </summary>
public enum BarStreamKind : byte
{
    Liquid,
    Fizzy,
    Cubes,
    Grains,
}

/// <summary>
/// One container stored in the dispenser and the drink it gives.
/// </summary>
public sealed class BarDrink
{
    public ItemStorageLocation Location;

    /// <summary>
    /// The container itself. Null until the client has learned which entity stands at the location.
    /// </summary>
    public EntityUid? Container;

    public string Name = string.Empty;
    public string Tip = string.Empty;
    public Color Color;
    public float Quantity;

    /// <summary>
    /// How much the container holds when full. Zero while that is not known.
    /// </summary>
    public float Capacity;

    public BarStreamKind Kind;

    /// <summary>
    /// Seconds for which the container goes on gurgling after a pour.
    /// </summary>
    public float Gurgle;
}

/// <summary>
/// Reads the state of one bar dispenser for its window.
/// </summary>
public sealed class BarDispenserModel
{
    /// <summary>
    /// Seconds for which a container gurgles after it has given some of its drink.
    /// </summary>
    public const float GurgleTime = 0.75f;

    private static readonly Comparison<BarDrink> Order = Compare;

    private readonly EntityUid _owner;
    private readonly BarDispenserSkinPrototype _skin;
    private readonly IEntityManager _entMan;
    private readonly IPrototypeManager _proto;
    private readonly IPlayerManager _player;
    private readonly SharedSolutionContainerSystem _solutions;
    private readonly SharedHandsSystem _hands;
    private readonly ItemSlotsSystem _slots;
    private readonly EntityWhitelistSystem _whitelist;
    private readonly List<BarDrink> _poured = new();
    private List<BarDrink> _drinks = new();
    private List<BarDrink> _spare = new();
    private EntityUid? _watched;
    private float _watchedVolume;
    private Color _watchedColor;

    /// <summary>
    /// The stored containers in the order of their names.
    /// </summary>
    public IReadOnlyList<BarDrink> Drinks => _drinks;

    /// <summary>
    /// Units poured by one press of a drink key.
    /// </summary>
    public ReagentDispenserDispenseAmount Amount { get; private set; } = ReagentDispenserDispenseAmount.U10;

    public EntityUid? Vessel { get; private set; }
    public Solution? VesselSolution { get; private set; }

    /// <summary>
    /// The thing in the player's hand, when it fits into the empty vessel slot.
    /// </summary>
    public EntityUid? HandVessel { get; private set; }

    /// <summary>
    /// Raised when the server says that a container has given some of its drink.
    /// </summary>
    public event Action<BarDrink>? OnPoured;

    /// <summary>
    /// Raised when a container has left the dispenser, while <see cref="Drinks"/> still lists it.
    /// </summary>
    public event Action<BarDrink>? OnRemoved;

    /// <summary>
    /// Raised when the vessel has been emptied. It carries the colour of what was in it.
    /// </summary>
    public event Action<Color>? OnDrained;

    public BarDispenserModel(EntityUid owner, BarDispenserSkinPrototype skin, IEntityManager entMan, IPrototypeManager proto, IPlayerManager player)
    {
        _owner = owner;
        _skin = skin;
        _entMan = entMan;
        _proto = proto;
        _player = player;
        _solutions = entMan.System<SharedSolutionContainerSystem>();
        _hands = entMan.System<SharedHandsSystem>();
        _slots = entMan.System<ItemSlotsSystem>();
        _whitelist = entMan.System<EntityWhitelistSystem>();
    }

    /// <summary>
    /// How many containers the dispenser stores, as far as the client knows before the server has listed them.
    /// </summary>
    public int StoredCount()
    {
        return _entMan.TryGetComponent(_owner, out StorageComponent? storage) ? storage.StoredItems.Count : 0;
    }

    /// <summary>
    /// Takes in what the server says about the dispenser.
    /// </summary>
    public void SetState(ReagentDispenserBoundUserInterfaceState state)
    {
        Amount = state.SelectedDispenseAmount;

        var next = _spare;
        next.Clear();
        _poured.Clear();
        foreach (var item in state.Inventory)
        {
            var container = ContainerAt(item.StorageLocation);
            var quantity = item.Quantity.Float();
            var drink = Find(item.StorageLocation, container);
            if (drink == null)
                drink = new BarDrink { Location = item.StorageLocation };
            else if (quantity < drink.Quantity)
                _poured.Add(drink);

            drink.Location = item.StorageLocation;
            drink.Container ??= container;
            drink.Name = item.ReagentLabel;
            drink.Color = item.ReagentColor.WithAlpha(1f);
            drink.Quantity = quantity;
            Measure(drink);
            next.Add(drink);
        }

        next.Sort(Order);
        foreach (var drink in _drinks)
        {
            if (!next.Contains(drink))
                OnRemoved?.Invoke(drink);
        }

        _spare = _drinks;
        _drinks = next;
        foreach (var drink in _poured)
        {
            drink.Gurgle = GurgleTime;
            OnPoured?.Invoke(drink);
        }
    }

    /// <summary>
    /// Reads the vessel and the player's hand again. Called once a frame.
    /// </summary>
    public void Refresh(float delta)
    {
        foreach (var drink in _drinks)
        {
            drink.Gurgle = MathF.Max(0f, drink.Gurgle - delta);
            if (drink.Capacity > 0f)
                continue;

            drink.Container ??= ContainerAt(drink.Location);
            Measure(drink);
        }

        Vessel = _slots.GetItemOrNull(_owner, SharedReagentDispenser.OutputSlotName);
        VesselSolution = null;
        HandVessel = null;
        if (Vessel is { } vessel)
        {
            if (_solutions.TryGetFitsInDispenser(vessel, out _, out var solution))
                VesselSolution = solution;
        }
        else if (_player.LocalEntity is { } user
                 && _hands.GetActiveItem(user) is { } held
                 && _slots.TryGetSlot(_owner, SharedReagentDispenser.OutputSlotName, out var slot)
                 && !slot.Locked
                 && _whitelist.CheckBoth(held, slot.Blacklist, slot.Whitelist))
        {
            HandVessel = held;
        }

        WatchVessel();
    }

    /// <summary>
    /// Whether the player has a hand to take a container with.
    /// </summary>
    public bool HasFreeHand()
    {
        return _player.LocalEntity is { } user && _hands.TryGetEmptyHand(user, out _);
    }

    private void WatchVessel()
    {
        var volume = VesselSolution?.Volume.Float() ?? 0f;
        if (Vessel != null && Vessel == _watched && _watchedVolume > 0f && volume <= 0f)
            OnDrained?.Invoke(_watchedColor);

        if (VesselSolution is { } solution && volume > 0f && (Vessel != _watched || volume != _watchedVolume))
            _watchedColor = solution.GetColor(_proto).WithAlpha(1f);

        _watched = Vessel;
        _watchedVolume = volume;
    }

    /// <summary>
    /// Where the drink's container stands right now. Players can move containers inside the storage,
    /// and the dispenser does not say so until something else changes.
    /// </summary>
    public ItemStorageLocation Locate(BarDrink drink)
    {
        if (drink.Container is { } container
            && _entMan.TryGetComponent(_owner, out StorageComponent? storage)
            && storage.StoredItems.TryGetValue(container, out var place))
        {
            drink.Location = place;
        }

        return drink.Location;
    }

    private BarDrink? Find(ItemStorageLocation location, EntityUid? container)
    {
        BarDrink? placed = null;
        foreach (var drink in _drinks)
        {
            if (container != null && drink.Container == container)
                return drink;

            if (drink.Location == location)
                placed = drink;
        }

        // Another container at the same place is another drink, not this one with less in it.
        return placed != null && (placed.Container == null || container == null) ? placed : null;
    }

    private EntityUid? ContainerAt(ItemStorageLocation location)
    {
        if (!_entMan.TryGetComponent(_owner, out StorageComponent? storage))
            return null;

        foreach (var (uid, place) in storage.StoredItems)
        {
            if (place == location)
                return uid;
        }

        return null;
    }

    private void Measure(BarDrink drink)
    {
        if (drink.Container is not { } container
            || _entMan.Deleted(container)
            || !_solutions.TryGetDrainableSolution(container, out _, out var solution))
        {
            return;
        }

        drink.Capacity = solution.MaxVolume.Float();
        if (solution.GetPrimaryReagentId() is not { } reagent)
            return;

        drink.Kind = _skin.Cubes.Contains(reagent.Prototype) ? BarStreamKind.Cubes
            : _skin.Grains.Contains(reagent.Prototype) ? BarStreamKind.Grains
            : _proto.TryIndex<ReagentPrototype>(reagent.Prototype, out var prototype) && prototype.Fizziness >= _skin.FizzFrom ? BarStreamKind.Fizzy
            : BarStreamKind.Liquid;
    }

    private static int Compare(BarDrink left, BarDrink right)
    {
        var names = string.Compare(left.Name, right.Name, StringComparison.CurrentCultureIgnoreCase);
        if (names != 0)
            return names;

        var rows = left.Location.Position.Y.CompareTo(right.Location.Position.Y);
        return rows != 0 ? rows : left.Location.Position.X.CompareTo(right.Location.Position.X);
    }
}
