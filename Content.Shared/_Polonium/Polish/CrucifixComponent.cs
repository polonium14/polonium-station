using Content.Shared.Chat.Prototypes;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Polish;

/// <summary>
/// Duży krzyż, do którego można przybić ciągniętą ofiarę. Port <c>/obj/structure/kitchenspike/crucifix</c> z NSV13.
/// Zwykłe przypinanie jest zablokowane: bezbronną ofiarę (w kajdankach albo nieprzytomną) przeciąga się na krzyż
/// lub klika krzyż, ciągnąc ją, a przybicie trwa <see cref="CrucifyDelay"/>.
/// Zejście trwa <see cref="FreeOtherDelay"/> (ktoś inny) albo <see cref="FreeSelfDelay"/> (samemu).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CrucifixComponent : Component
{
    [DataField]
    public TimeSpan CrucifyDelay = TimeSpan.FromSeconds(12);

    [DataField]
    public TimeSpan FreeOtherDelay = TimeSpan.FromSeconds(30);

    [DataField]
    public TimeSpan FreeSelfDelay = TimeSpan.FromSeconds(120);

    /// <summary>
    /// Obrażenia przy przybiciu, przy próbie samodzielnego zejścia i przy upadku z krzyża.
    /// </summary>
    [DataField(required: true)]
    public DamageSpecifier Damage = new();

    /// <summary>
    /// Na jak długo ofiara pada po zdjęciu z krzyża.
    /// </summary>
    [DataField]
    public TimeSpan FallParalyze = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Emote krzyku ofiary.
    /// </summary>
    [DataField]
    public ProtoId<EmotePrototype> ScreamEmote = "Scream";

    /// <summary>
    /// Ustawiane na czas przypinania/odpinania przez sam system krzyża, żeby ominąć blokadę zwykłego przypinania.
    /// </summary>
    [ViewVariables]
    public bool Bypass;
}

[Serializable, NetSerializable]
public sealed partial class CrucifyDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class CrucifixFreeDoAfterEvent : SimpleDoAfterEvent;
