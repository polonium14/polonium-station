using Content.Shared.EntityTable.EntitySelectors;

namespace Content.Server._Polonium.Abductors;

[RegisterComponent, Access(typeof(AbductorGlandDispenserSystem))]
public sealed partial class AbductorGlandDispenserComponent : Component
{
    [DataField(required: true)]
    public EntityTableSelector Table = default!;
}
