using Content.Shared.GameTicking.Components;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Polonium.GameTicking;

/// <summary>
/// Remembers when an ended game rule was activated, because ending a rule removes its <see cref="GameRuleComponent"/>.
/// </summary>
[RegisterComponent, Access(typeof(FinishedGameRuleSystem))]
public sealed partial class FinishedGameRuleComponent : Component
{
    /// <summary>
    /// Round time the rule was activated at.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan ActivatedAt;
}
