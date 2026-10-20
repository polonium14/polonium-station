using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Polish;

/// <summary>
/// Przedmiot, który po kliknięciu na ścianę zawiesza się na niej jako <see cref="Structure"/>
/// (krzyże i godła z NSV13, tamtejsze <c>/obj/item/wallframe</c>).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class WallHangableComponent : Component
{
    /// <summary>
    /// Ozdoba stawiana na ścianie.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Structure;
}

/// <summary>
/// Ozdoba wisząca na ścianie, którą można zdjąć pustą ręką z powrotem jako <see cref="Item"/>.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class WallHangingComponent : Component
{
    /// <summary>
    /// Przedmiot oddawany po zdjęciu ze ściany.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Item;
}
