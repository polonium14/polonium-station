using System.Collections.Generic;
using System.Linq;
using Content.Client.Lobby;
using Robust.Client.UserInterface.Controllers;

namespace Content.Client.Administration.UI.EntitySearch;

/// <summary>
///     Session-scoped store of entities pinned from the admin entity search window. Outlives the window instance so
///     pins survive closing/reopening the search (but not a reconnect, since NetEntity ids are per-session).
///     Snapshots are cached verbatim (never refreshed), so actions on a since-deleted entity simply no-op server-side.
/// </summary>
public sealed class PinnedEntitiesUIController : UIController, IOnStateEntered<LobbyState>
{
    private readonly List<(string name, string? proto, NetEntity entity)> _pinned = new();

    public IReadOnlyList<(string name, string? proto, NetEntity entity)> Pinned => _pinned;

    public event Action? Changed;

    /// <summary>Raised when the round ends (lobby entered); listeners should clear their search state too.</summary>
    public event Action? Reset;

    public bool IsPinned(NetEntity entity) => _pinned.Any(p => p.entity == entity);

    public void Toggle(string name, string? proto, NetEntity entity)
    {
        var idx = _pinned.FindIndex(p => p.entity == entity);
        if (idx >= 0)
            _pinned.RemoveAt(idx);
        else
            _pinned.Add((name, proto, entity));

        Changed?.Invoke();
    }

    // Drop pinned entities as round ends since they go stale.
    public void OnStateEntered(LobbyState state)
    {
        _pinned.Clear();
        Changed?.Invoke();
        Reset?.Invoke();
    }
}
