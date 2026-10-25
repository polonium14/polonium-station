using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.GameTicking;

/// <summary>
/// A round mood players can vote for. It nudges secret towards the presets that list it.
/// </summary>
[Prototype]
public sealed partial class RoundMoodPrototype : IPrototype
{
    [IdDataField, ViewVariables]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    /// <summary>
    /// The mood only shows up in the vote with at least this many players connected.
    /// </summary>
    [DataField]
    public int MinPlayers;

    /// <summary>
    /// Position among the vote options, lowest first.
    /// </summary>
    [DataField]
    public int Order;

    /// <summary>
    /// The mood used when there was no vote. People who didn't vote count towards it too.
    /// </summary>
    [DataField]
    public bool Default;
}
