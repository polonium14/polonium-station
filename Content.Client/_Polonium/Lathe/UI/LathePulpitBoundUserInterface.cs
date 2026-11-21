using System.Numerics;
using Content.Shared._Polonium.Lathe;
using Content.Shared.Lathe;
using Content.Shared.Research.Components;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;

namespace Content.Client._Polonium.Lathe.UI;

[UsedImplicitly]
public sealed class LathePulpitBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    public LathePulpitWindow? Window { get; private set; }

    protected override void Open()
    {
        base.Open();

        Window = this.CreateWindow<LathePulpitWindow>();
        Window.SetEntity(Owner);

        // The lube tank widens the window after it was centred.
        if (!EntMan.System<UserInterfaceSystem>().TryGetPosition(Owner, UiKey, out _))
        {
            Window.Measure(Vector2Helpers.Infinity);
            Window.RecenterWindow(new Vector2(0.5f, 0.5f));
        }
        Window.OnQueue += (recipe, amount) => SendMessage(new LatheQueueRecipeMessage(recipe, amount));
        Window.OnDelete += index => SendMessage(new LatheDeleteRequestMessage(index));
        Window.OnMove += (index, change) => SendMessage(new LatheMoveRequestMessage(index, change));
        Window.OnAbort += () => SendMessage(new LatheAbortFabricationMessage());
        Window.OnStopAll += () => SendMessage(new LatheStopAllMessage());
        Window.OnServerList += () => SendMessage(new ConsoleServerSelectionMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is LatheUpdateState update)
            Window?.UpdateState(update);
    }
}
