using Content.Shared._Polonium.Tutorial;
using Content.Shared._Polonium.Tutorial.Components;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Polonium.Tutorial.UI;

[UsedImplicitly]
public sealed class TutorialAnchorLabelerBoundUserInterface : BoundUserInterface
{
    private TutorialAnchorLabelerWindow? _window;

    public TutorialAnchorLabelerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<TutorialAnchorLabelerWindow>();

        if (EntMan.TryGetComponent(Owner, out TutorialAnchorLabelerComponent? labeler))
            _window.SetMaxAnchorLength(labeler.MaxAnchorChars);

        _window.OnAnchorChanged += OnAnchorChanged;
        Reload();
        _window.SetInitialAnchorState();
    }

    private void OnAnchorChanged(string newAnchor)
    {
        if (EntMan.TryGetComponent(Owner, out TutorialAnchorLabelerComponent? labeler) &&
            labeler.AssignedAnchor.Equals(newAnchor))
            return;

        SendPredictedMessage(new TutorialAnchorLabelerAnchorChangedMessage(newAnchor));
    }

    public void Reload()
    {
        if (_window == null || !EntMan.TryGetComponent(Owner, out TutorialAnchorLabelerComponent? component))
            return;

        _window.SetCurrentAnchor(component.AssignedAnchor);
    }
}
