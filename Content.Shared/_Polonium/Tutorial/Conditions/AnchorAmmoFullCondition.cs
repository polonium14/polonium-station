namespace Content.Shared._Polonium.Tutorial.Conditions;

/// <summary>
/// At least <see cref="Count"/> of the anchors sharing this id are loaded to capacity - magazines,
/// ammo boxes, anything that answers the ammo count.
/// </summary>
public sealed partial class AnchorAmmoFullCondition : TutorialCondition
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;

    [DataField]
    public int Count = 1;
}
