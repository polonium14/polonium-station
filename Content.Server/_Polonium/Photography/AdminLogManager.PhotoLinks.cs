using System.Collections.Generic;
using Content.Shared._Polonium.Photography;

namespace Content.Server.Administration.Logs;

/// <summary>
/// POLONIUM CHANGE!
/// Photography half of <see cref="AdminLogManager"/>: turns a <see cref="LoggablePhotoId"/> carried
/// in a capture log's structured values into a clickable admin-chat link that opens the photo viewer
/// focused on it. Kept in a partial file under _Polonium so the only edit to the upstream file is the
/// one call site in <c>DoAdminAlerts</c>. Mirrors the shape of <c>GetCoordinates</c>/<c>CreateCordLinks</c>.
/// </summary>
public sealed partial class AdminLogManager
{
    /// <summary>Pulls captured-photo ids out of a log's structured values (by type, like GetCoordinates).</summary>
    private List<int> GetPhotoIds(Dictionary<string, object?> values)
    {
        var ids = new List<int>();
        foreach (var value in values.Values)
        {
            if (value is LoggablePhotoId photo)
                ids.Add(photo.Id);
        }

        return ids;
    }

    /// <summary>Builds cmdlinks that open the admin photo viewer focused on each id (mirrors CreateTpLinks).</summary>
    private bool CreatePhotoLinks(List<int> photoIds, out string outString)
    {
        outString = string.Empty;

        if (photoIds.Count == 0)
            return false;

        outString = Loc.GetString("admin-alert-photo-header");

        for (var i = 0; i < photoIds.Count; i++)
        {
            outString += $"[cmdlink=\"{photoIds[i]}\" command=\"photos {photoIds[i]}\"/]";

            if (i < photoIds.Count - 1)
                outString += ", ";
        }

        return true;
    }
}
