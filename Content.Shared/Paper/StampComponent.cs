// SPDX-FileCopyrightText: 2022 Fishfish458 <47410468+Fishfish458@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022 fishfish458 <fishfish458>
// SPDX-FileCopyrightText: 2022 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 Alex <129697969+Lomcastar@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 crazybrain23 <44417085+crazybrain23@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 eoineoineoin <github@eoinrul.es>
// SPDX-FileCopyrightText: 2023 lzk <124214523+lzk228@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Aiden <aiden@djkraz.com>
// SPDX-FileCopyrightText: 2024 TsjipTsjip <19798667+TsjipTsjip@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 V <97265903+formlessnameless@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 corresp0nd <46357632+corresp0nd@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 taydeo <tay@funkystation.org>
// SPDX-FileCopyrightText: 2026 taydeo <td12233a@gmail.com>
//
// SPDX-License-Identifier: MIT

using System.Numerics;
using Robust.Shared.Serialization;
using Robust.Shared.Audio;

namespace Content.Shared.Paper;

/// <summary>
///     Set of required information to draw a stamp in UIs, where
///     representing the state of the stamp at the point in time
///     when it was applied to a paper. These fields mirror the
///     equivalent in the component.
/// </summary>
[DataDefinition, Serializable, NetSerializable]
public partial struct StampDisplayInfo
{
    StampDisplayInfo(string s)
    {
        StampedName = s;
    }

    [DataField("stampedName")]
    public string StampedName;

    [DataField("stampedColor")]
    public Color StampedColor;

    [DataField("stampLargeIcon")]
    public string? StampLargeIcon; // imp

    [DataField("stampFont")]
    public string? StampFont; // imp

    [DataField("hasIcon")]
    public bool HasIcon = true; // imp

    /// <summary>
    ///     Whether <see cref="StampedName"/> is a localization id to be run through
    ///     Loc.GetString (true, for stamps whose name is a loc key), or a literal
    ///     display string to show verbatim (false, for signatures whose name is a
    ///     raw signer name). Localizing a raw name spams warnings and can collide
    ///     with a real Fluent id.
    /// </summary>
    [DataField("localizeName")]
    public bool LocalizeName = true;

    /// <summary>
    ///     Normalized [0,1] position within the stamp display area where this
    ///     mark was placed. Null means "use the procedural auto-layout".
    ///     Used by manually-placed signatures.
    /// </summary>
    [DataField("position")]
    public Vector2? Position;

    /// <summary>
    ///     Scale multiplier applied to the mark's natural size. Null means 1x.
    /// </summary>
    [DataField("scale")]
    public float? Scale;

    /// <summary>
    ///     Explicit orientation in radians. Null means the auto-layout picks a
    ///     small random tilt.
    /// </summary>
    [DataField("rotation")]
    public float? Rotation;
};

[RegisterComponent]
public sealed partial class StampComponent : Component
{
    /// <summary>
    ///     The loc string name that will be stamped to the piece of paper on examine.
    /// </summary>
    [DataField("stampedName")]
    public string StampedName { get; set; } = "stamp-component-stamped-name-default";

    /// <summary>
    ///     The sprite state of the stamp to display on the paper from paper Sprite path.
    /// </summary>
    [DataField("stampState")]
    public string StampState { get; set; } = "paper_stamp-generic";

    /// <summary>
    ///     The sprite state of the stamp to display on the paper when read from stamp Sprite path.
    /// </summary>
    [DataField("stampLargeIcon")]
    public string? StampLargeIcon = null; // imp

    /// <summary>
    /// The color of the ink used by the stamp in UIs
    /// </summary>
    [DataField("stampedColor")]
    public Color StampedColor = Color.FromHex("#BB3232"); // StyleNano.DangerousRedFore

    /// <summary>
    /// The sound when stamp stamped
    /// </summary>
    [DataField("sound")]
    public SoundSpecifier? Sound = null;
}
