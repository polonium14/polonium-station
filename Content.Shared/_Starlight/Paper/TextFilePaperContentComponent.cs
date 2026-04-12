// SPDX-FileCopyrightText: 2026 beck-thompson <107373427+beck-thompson@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 beck-thompson <beck314159@hotmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Paper;

/// <summary>
///     Load text into the document from the given text file.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class TextFilePaperContentComponent : Component
{
    /// <summary>
    ///     Name of the file to load in located in the Documents folder in resources.
    /// </summary>
    [DataField(required: true)]
    public string FileName = "";
}
