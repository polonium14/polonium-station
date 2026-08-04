// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Paper;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SignatureWriterComponent : Component
{
    /// <summary>
    /// If this is set, the defined font will be forced for the signature.
    /// Networked so the client preview matches the server-committed signature.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? Font;

    /// <summary>
    /// The list of fonts that can be selected from, for pens with multiple fonts.
    /// </summary>
    [DataField]
    public Dictionary<string, string> FontList = new();

    /// <summary>
    /// The color used for the signature. Networked so the client preview matches
    /// the server-committed signature.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Color Color = Color.FromHex("#2F4F4F");

    /// <summary>
    /// The list of colors that can be selected from, for pens with multiple colors.
    /// </summary>
    [DataField]
    public Dictionary<string, Color> ColorList = new();
}
