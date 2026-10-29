using Content.Shared._Polonium.Modules.Components;
using Content.Shared._Polonium.Modules.Events;

namespace Content.Shared._Polonium.Modules;

public partial class SharedModuleSystem
{
    private void InitializeRequirements()
    {
        SubscribeLocalEvent<ModuleExclusivityComponent, ModuleAddingAttemptEvent>(OnExclusivityAdd);
    }

    private void OnExclusivityAdd(Entity<ModuleExclusivityComponent> ent, ref ModuleAddingAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        var matches = 0;

        foreach (var module in GetModules(args.Container))
        {
            if (!_whitelist.CheckBoth(module, ent.Comp.Blacklist, ent.Comp.Whitelist))
                matches++;
        }

        if (matches >= ent.Comp.Maximum || matches < ent.Comp.Minimum)
        {
            args.Reason = ent.Comp.Popup;
            args.Cancel();
        }
    }
}
