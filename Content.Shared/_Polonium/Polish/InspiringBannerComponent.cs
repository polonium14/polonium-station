using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Polonium.Polish;

/// <summary>
/// Sztandar, którym można zamachać w dłoni: posiadacz wznosi okrzyk bojowy, leczy się i otrząsa z ogłuszenia.
/// Port <c>/obj/item/banner</c> z NSV13 (Aquila).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class InspiringBannerComponent : Component
{
    /// <summary>
    /// Okrzyk wypowiadany przy machaniu sztandarem. Bez okrzyku posiadacz nic nie mówi.
    /// </summary>
    [DataField]
    public LocId? Warcry;

    /// <summary>
    /// Czas między kolejnymi użyciami.
    /// </summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Kiedy sztandar znowu zadziała.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextUse;

    /// <summary>
    /// Ile obrażeń z każdej grupy leczy posiadaczowi (rozłożone po równo na typy w grupie).
    /// </summary>
    [DataField(required: true)]
    public Dictionary<ProtoId<DamageGroupPrototype>, FixedPoint2> Heal = new();

    /// <summary>
    /// O ile skraca się ogłuszenie i powalenie posiadacza.
    /// </summary>
    [DataField]
    public TimeSpan StunReduction = TimeSpan.FromSeconds(4);

    /// <summary>
    /// Szelest sztandaru.
    /// </summary>
    [DataField]
    public SoundSpecifier FlourishSound = new SoundPathSpecifier("/Audio/Effects/rustle1.ogg");

    /// <summary>
    /// Dźwięk leczenia.
    /// </summary>
    [DataField]
    public SoundSpecifier HealSound = new SoundPathSpecifier("/Audio/Magic/staff_healing.ogg");
}
