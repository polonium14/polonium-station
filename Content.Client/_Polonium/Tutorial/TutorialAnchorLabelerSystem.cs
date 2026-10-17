using Content.Client._Polonium.Tutorial.UI;
using Content.Shared._Polonium.Tutorial;
using Content.Shared._Polonium.Tutorial.Components;

namespace Content.Client._Polonium.Tutorial;

public sealed class TutorialAnchorLabelerSystem : SharedTutorialAnchorLabelerSystem
{
    protected override void UpdateUI(Entity<TutorialAnchorLabelerComponent> ent)
    {
        if (UserInterfaceSystem.TryGetOpenUi(ent.Owner, TutorialAnchorLabelerUiKey.Key, out var bui)
            && bui is TutorialAnchorLabelerBoundUserInterface labeler)
        {
            labeler.Reload();
        }
    }
}
