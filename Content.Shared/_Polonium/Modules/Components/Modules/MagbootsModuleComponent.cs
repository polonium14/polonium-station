using Content.Shared._Polonium.Modules.ModSuit;
using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Modules.Components.Modules;

[RegisterComponent, NetworkedComponent]
public sealed partial class MagbootsModuleComponent : Component
{
    [DataField]
    public ModSuitPartType? ModSuitPart;
}
