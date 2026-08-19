using System.Collections.Generic;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Shared._Polonium.Photography;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.Eui;
using JetBrains.Annotations;

namespace Content.Server._Polonium.Photography;

/// <summary>Server half of the admin photo viewer.</summary>
[UsedImplicitly]
public sealed partial class AdminPhotoEui : BaseEui
{
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IAdminManager _admin = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;

    private readonly PoloniumPhotographySystem _photography;

    private int? _selected;

    public AdminPhotoEui(int? focus = null)
    {
        IoCManager.InjectDependencies(this);
        _photography = _entMan.System<PoloniumPhotographySystem>();
        _selected = focus;
    }

    public override void Opened()
    {
        base.Opened();
        StateDirty();
    }

    public override EuiStateBase GetNewState()
    {
        var photos = new List<AdminPhotoEntry>();
        foreach (var (id, shooter, subject) in _photography.GetStoredPhotos())
        {
            photos.Add(new AdminPhotoEntry { Id = id, Shooter = shooter, Subject = subject });
        }

        var data = _selected is { } sel ? _photography.GetStoredPhoto(sel) : null;
        if (data == null)
            _selected = null;

        return new AdminPhotoEuiState(photos, _selected, data);
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (!_admin.HasAdminFlag(Player, AdminFlags.Admin))
            return;

        switch (msg)
        {
            case AdminPhotoSelectMessage select:
                _selected = select.PhotoId;
                StateDirty();
                break;

            case AdminPhotoDeleteMessage delete:
                if (_photography.DeleteStoredPhoto(delete.PhotoId))
                {
                    _adminLog.Add(LogType.Action, LogImpact.High,
                        $"{Player.Name} deleted stored photo id {delete.PhotoId}");
                }

                if (_selected == delete.PhotoId)
                    _selected = null;
                StateDirty();
                break;
        }
    }
}
