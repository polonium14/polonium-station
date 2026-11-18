using System.Diagnostics.CodeAnalysis;
using Content.Server._Polonium.Contributors;
using Content.Server._Polonium.Supporters;
using Content.Server.Administration.Managers;
using Robust.Shared.Player;

namespace Content.Server._Polonium.Chat;

/// <summary>
/// Picks the color of a player's name in OOC and LOOC: supporters before contributors.
/// </summary>
public sealed partial class ChatNameColorSystem : EntitySystem
{
    [Dependency] private IAdminManager _admin = default!;
    [Dependency] private ContributorManager _contributors = default!;
    [Dependency] private SupporterManager _supporters = default!;

    public bool TryGetNameColor(ICommonSession player, [NotNullWhen(true)] out string? color)
    {
        if (_supporters.TryGetNameColor(player.UserId, out color))
            return true;

        if (_admin.IsAdmin(player))
        {
            color = null;
            return false;
        }

        return _contributors.TryGetNameColor(player.UserId, out color);
    }
}
