namespace Content.Server._Polonium.Body;

/// <summary>
/// Gives an organ a sale price once it has been part of a body, so organs straight from the biofab sell for nothing.
/// </summary>
[RegisterComponent]
[Access(typeof(OrganPriceSystem))]
public sealed partial class OrganPriceComponent : Component
{
    /// <summary>
    /// What the organ is worth outside a body after it has been in one, at full integrity.
    /// With <see cref="MaxPrice"/> set, this is the lower bound of the roll and gets replaced by the rolled price.
    /// </summary>
    [DataField(required: true)]
    public double Price;

    /// <summary>
    /// If set, the price is rolled once between <see cref="Price"/> and this when the organ first goes into a body.
    /// </summary>
    [DataField]
    public double? MaxPrice;

    /// <summary>
    /// Whether the organ has ever been in a body.
    /// </summary>
    [DataField]
    public bool Harvested;
}
