using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Medical.Surgery;

/// <summary>
/// The patient feels no pain: surgery on them hurts as little as on a sleeping one and goes faster.
/// Use only on a status effect entity.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AnalgesiaStatusEffectComponent : Component;
