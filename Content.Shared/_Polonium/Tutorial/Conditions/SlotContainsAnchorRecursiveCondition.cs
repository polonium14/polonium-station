namespace Content.Shared._Polonium.Tutorial.Conditions;

/// <summary>
/// Done when the player's inventory slot (and any container inside it) contains all listed anchors.
/// Useful for "put card into PDA and PDA into ID slot".
/// </summary>
public sealed partial class SlotContainsAnchorRecursiveCondition : TutorialCondition
{
    /// <summary>Inventory slot to start searching from, e.g. "id".</summary>
    [DataField(required: true)]
    public string Slot = string.Empty;

    /// <summary>All these anchors must be found somewhere inside (recursively).</summary>
    [DataField(required: true)]
    public List<string> AnchorIds = new();

    /// <summary>
    /// Only accept the named slot. With this off the search falls back to hands and the rest of
    /// the inventory, which quietly turns "put it in your bag" into "hold it".
    /// </summary>
    [DataField]
    public bool Strict;
}
