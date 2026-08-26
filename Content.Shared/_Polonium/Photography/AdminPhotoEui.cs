using System.Collections.Generic;
using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Photography;

/// <summary>State for the admin photo viewer.</summary>
[Serializable, NetSerializable]
public sealed class AdminPhotoEuiState : EuiStateBase
{
    public readonly List<AdminPhotoEntry> Photos;
    public readonly int? SelectedId;
    public readonly byte[]? SelectedData;

    public AdminPhotoEuiState(List<AdminPhotoEntry> photos, int? selectedId, byte[]? selectedData)
    {
        Photos = photos;
        SelectedId = selectedId;
        SelectedData = selectedData;
    }
}

[Serializable, NetSerializable]
public struct AdminPhotoEntry
{
    public int Id;
    public string Shooter;
    public string? Subject;
}

/// <summary>Admin asked to view a specific stored photo.</summary>
[Serializable, NetSerializable]
public sealed class AdminPhotoSelectMessage : EuiMessageBase
{
    public readonly int PhotoId;

    public AdminPhotoSelectMessage(int photoId)
    {
        PhotoId = photoId;
    }
}

/// <summary>Admin asked to delete a stored photo (and the photograph entity holding it).</summary>
[Serializable, NetSerializable]
public sealed class AdminPhotoDeleteMessage : EuiMessageBase
{
    public readonly int PhotoId;

    public AdminPhotoDeleteMessage(int photoId)
    {
        PhotoId = photoId;
    }
}
