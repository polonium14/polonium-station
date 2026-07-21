namespace Content.Shared._Shitmed.Body;

/// <summary>
/// Transient marker added to an organ immediately around a BodyDamageBridgeSystem
/// mob-&gt;organ fan-out write. Tells BodyDamageBridgeSystem's own organ-&gt;mob sync
/// (see OnOrganDamageChanged) that this particular organ damage change originated from
/// the mob's side and was already applied there directly by DamageableSystem.Events'
/// InjurableComponent handler - propagating it back up would double-count it.
/// </summary>
[RegisterComponent]
public sealed partial class SkipOrganMobSyncComponent : Component;
