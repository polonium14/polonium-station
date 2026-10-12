using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Tag;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Polish;

/// <summary>
/// Niedźwiedź Wojtek: zjada podane papierosy i wypija piwo z podanego naczynia, lecząc się przy tym.
/// Port <c>/mob/living/simple_animal/hostile/retaliate/wojtek</c> z NSV13.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class WojtekComponent : Component
{
    /// <summary>
    /// Tag papierosów, które Wojtek zjada.
    /// </summary>
    [DataField]
    public ProtoId<TagPrototype> CigaretteTag = "Cigarette";

    /// <summary>
    /// Leczenie po zjedzeniu papierosa.
    /// </summary>
    [DataField]
    public FixedPoint2 CigaretteHeal = 10;

    /// <summary>
    /// Reagent, po który Wojtek sięga.
    /// </summary>
    [DataField]
    public ProtoId<ReagentPrototype> Beer = "Beer";

    /// <summary>
    /// Leczenie po wypiciu piwa.
    /// </summary>
    [DataField]
    public FixedPoint2 BeerHeal = 20;

    /// <summary>
    /// Grupa obrażeń leczona przez papierosy i piwo.
    /// </summary>
    [DataField]
    public ProtoId<DamageGroupPrototype> HealGroup = "Brute";

    [DataField]
    public SoundSpecifier DrinkSound = new SoundPathSpecifier("/Audio/Items/drink.ogg");

    /// <summary>
    /// Dźwięk odtwarzany przy śmierci Wojtka.
    /// </summary>
    [DataField]
    public SoundSpecifier? DeathSound;
}
