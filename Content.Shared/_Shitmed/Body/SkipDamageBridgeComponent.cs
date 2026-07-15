namespace Content.Shared._Shitmed.Body;

/// <summary>
/// Transient marker: added immediately before a mob-level TryChangeDamage call that must NOT
/// be picked up by BodyDamageBridgeSystem, then removed right after. Needed because "no
/// TargetingComponent-bearing origin" is ambiguous by itself - it's both what a deliberate
/// bridge-bypass call (e.g. SurgerySystem.SetDamage's mob-level reflect, which already applied
/// the same damage to a specific organ directly and doesn't want it re-applied via a random
/// limb split) and a genuinely untargeted environmental source (fire, chemicals) look like from
/// the bridge's perspective. The marker disambiguates the former so the latter can still fall
/// through to BodyDamageBridgeSystem's even-limb-split fallback.
/// </summary>
[RegisterComponent]
public sealed partial class SkipDamageBridgeComponent : Component;
