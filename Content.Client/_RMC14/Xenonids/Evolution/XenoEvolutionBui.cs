using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Shared.FixedPoint;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client._RMC14.Xenonids.Evolution;

[UsedImplicitly]
public sealed partial class XenoEvolutionBui : BoundUserInterface
{
    [Dependency] private IPrototypeManager _prototypes = default!;

    private readonly SpriteSystem _sprite;
    private readonly XenoEvolutionSystem _evolution;

    private XenoEvolutionWindow? _window;

    // dont rebuild buttons every points tick - that eats clicks
    private bool? _listingsReady;

    public XenoEvolutionBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _sprite = EntMan.System<SpriteSystem>();
        _evolution = EntMan.System<XenoEvolutionSystem>();
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<XenoEvolutionWindow>();
        _listingsReady = null;
        Refresh(forceListings: true);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        Refresh(forceListings: true);
    }

    public void Refresh(bool forceListings = false)
    {
        if (_window == null)
            return;

        if (!EntMan.TryGetComponent(Owner, out XenoEvolutionComponent? evolution))
            return;

        var ready = evolution.Max <= FixedPoint2.Zero || evolution.Points >= evolution.Max;
        _window.SetPoints(evolution.Points, evolution.Max, ready);

        if (!forceListings && _listingsReady == ready)
            return;

        _listingsReady = ready;
        _window.ClearListings();

        foreach (var choice in evolution.EvolvesToWithoutPoints)
        {
            var canBuy = _evolution.CanEvolvePopup((Owner, evolution), choice, doPopup: false);
            AddListing(choice, canBuy, cost: null);
        }

        foreach (var choice in evolution.EvolvesTo)
        {
            var canBuy = ready && _evolution.CanEvolvePopup((Owner, evolution), choice, doPopup: false);
            AddListing(choice, canBuy, cost: evolution.Max);
        }
    }

    private void AddListing(EntProtoId choice, bool canBuy, FixedPoint2? cost)
    {
        if (_window == null || !_prototypes.TryIndex(choice, out var proto))
            return;

        var texture = _sprite.GetPrototypeIcon(choice).Default;
        var price = cost == null
            ? Loc.GetString("rmc-xeno-evolution-ui-price-free")
            : Loc.GetString("rmc-xeno-evolution-ui-price", ("amount", (int) Math.Floor(cost.Value.Double())));

        var description = string.IsNullOrWhiteSpace(proto.Description)
            ? Loc.GetString("rmc-xeno-evolution-ui-no-desc")
            : proto.Description;

        var listing = new XenoEvolutionListingControl(
            proto.Name,
            description,
            price,
            canBuy,
            texture);

        listing.BuyButton.OnPressed += _ =>
        {
            SendPredictedMessage(new XenoEvolveBuiMsg(choice));
            Close();
        };

        _window.AddListing(listing);
    }
}
