using Content.Shared._Polonium.Modules.ModSuit.UI;

namespace Content.Shared._Polonium.Modules.ModSuit.Events;

public sealed partial class ModSuitGetUiEntriesEvent : EntityEventArgs
{
    public List<BaseModSuitBuiEntry> Entries = [];
}
