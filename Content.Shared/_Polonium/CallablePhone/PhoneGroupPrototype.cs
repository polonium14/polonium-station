using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.CallablePhone;

/// <summary>
/// A callable phone network group. Phones dial each other when the caller's
/// <see cref="CallablePhoneComponent.DialableGroups"/> overlaps the receiver's
/// <see cref="CallablePhoneComponent.PhoneGroups"/>.
/// </summary>
[Prototype("phoneGroup")]
public sealed partial class PhoneGroupPrototype : IPrototype
{
    [IdDataField, ViewVariables]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Human-readable name for the group.
    /// </summary>
    [DataField("name")]
    public LocId Name { get; private set; } = string.Empty;
}
