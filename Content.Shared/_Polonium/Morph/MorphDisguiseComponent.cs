using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Morph;

/// <summary>
/// Marks a chameleon disguise entity used by a morph.
/// Handles the ambush on touch and the "doesn't look right" examine text.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MorphDisguiseComponent : Component;
