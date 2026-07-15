using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;

/// <summary>
/// Integrity tracking for a vital organ (heart, liver, eyes, etc — NOT a limb, which uses
/// WoundableComponent instead).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OrganIntegrityComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public FixedPoint2 IntegrityCap;

    [ViewVariables, AutoNetworkedField]
    public FixedPoint2 OrganIntegrity;

    [DataField(required: true)]
    public Dictionary<OrganSeverity, FixedPoint2> IntegrityThresholds = new();

    [ViewVariables, AutoNetworkedField]
    public OrganSeverity OrganSeverity = OrganSeverity.Normal;

    [ViewVariables]
    public Dictionary<(string, EntityUid), FixedPoint2> IntegrityModifiers = new();

    [DataField]
    public SoundSpecifier OrganDestroyedSound = new SoundCollectionSpecifier("OrganDestroyed");
}
