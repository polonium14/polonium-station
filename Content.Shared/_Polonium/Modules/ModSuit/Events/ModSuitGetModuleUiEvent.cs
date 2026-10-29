using Content.Shared._Polonium.Modules.ModSuit.UI.Modules;

namespace Content.Shared._Polonium.Modules.ModSuit.Events;

public sealed partial class ModSuitGetModuleUiEvent : EntityEventArgs
{
    public List<ModSuitBaseModuleBuiEntry> BuiEntries = [];
}
