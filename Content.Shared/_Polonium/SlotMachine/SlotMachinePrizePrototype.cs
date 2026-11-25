// Ported from Goob-Station (https://github.com/Goob-Station/Goob-Station/pull/4929), AGPL-3.0-or-later.

using Content.Shared.EntityTable.EntitySelectors;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.SlotMachine;

/// <summary>
/// One possible outcome of a slot machine spin, including losing.
/// </summary>
[Prototype]
public sealed partial class SlotMachinePrizePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Relative chance of this outcome against the other prizes of the machine.
    /// </summary>
    [DataField(required: true)]
    public float Weight;

    /// <summary>
    /// What gets spawned on the machine. Null means nothing is paid out.
    /// </summary>
    [DataField]
    public EntityTableSelector? PrizeTable;

    /// <summary>
    /// What the machine says out loud when this outcome is rolled.
    /// </summary>
    [DataField]
    public LocId? WinMessage;

    /// <summary>
    /// Played when this outcome is rolled.
    /// </summary>
    [DataField]
    public SoundSpecifier WinSound = new SoundPathSpecifier("/Audio/Effects/Arcade/win.ogg");
}
