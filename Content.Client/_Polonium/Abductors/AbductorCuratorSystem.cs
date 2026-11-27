using Content.Shared._Polonium.Abductors;
using Robust.Client.UserInterface;

namespace Content.Client._Polonium.Abductors;

public sealed partial class AbductorCuratorSystem : EntitySystem
{
    [Dependency] private IUserInterfaceManager _ui = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<AbductorCuratorSpeechEvent>(OnSpeech);
    }

    private void OnSpeech(AbductorCuratorSpeechEvent ev)
    {
        var controller = _ui.GetUIController<AbductorCuratorUIController>();
        foreach (var page in ev.Pages)
        {
            controller.Say(Loc.GetString(page));
        }
    }
}
