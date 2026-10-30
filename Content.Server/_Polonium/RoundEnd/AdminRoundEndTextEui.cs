using Content.Server.Administration;
using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Shared._Polonium.RoundEnd;
using Content.Shared.Administration;
using Content.Shared.Eui;

namespace Content.Server._Polonium.RoundEnd;

public sealed partial class AdminRoundEndTextEui : BaseEui
{
    [Dependency] private IAdminManager _admins = default!;
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private ILogManager _logManager = default!;

    private readonly AdminRoundEndTextSystem _roundEndText;
    private readonly ISawmill _sawmill;

    public AdminRoundEndTextEui(IDependencyCollection deps)
    {
        deps.InjectDependencies(this, oneOff: true);

        _roundEndText = _entities.System<AdminRoundEndTextSystem>();
        _sawmill = _logManager.GetSawmill("admin-round-end-text");
    }

    public override void Opened()
    {
        base.Opened();
        _roundEndText.Register(this);
        _admins.OnPermsChanged += OnPermsChanged;
        StateDirty();
    }

    public override void Closed()
    {
        base.Closed();
        _roundEndText.Unregister(this);
        _admins.OnPermsChanged -= OnPermsChanged;
    }

    private void OnPermsChanged(AdminPermsChangedEventArgs args)
    {
        if (args.Player == Player && !_admins.HasAdminFlag(Player, AdminFlags.Round))
            Close();
    }

    public override EuiStateBase GetNewState()
    {
        return new AdminRoundEndTextEuiState(
            _roundEndText.Text,
            _roundEndText.Editor,
            _roundEndText.EditTime,
            _roundEndText.CanEdit,
            _roundEndText.GetRulesWithText());
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (!_admins.HasAdminFlag(Player, AdminFlags.Round))
        {
            Close();
            return;
        }

        if (msg is not AdminRoundEndTextEuiMsg.Save save)
            return;

        if (!_roundEndText.TrySetText(Player, save.Text))
            _sawmill.Warning($"{Player} sent a round end text that could not be set");

        StateDirty();
    }
}
