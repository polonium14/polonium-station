using Content.Shared.Objectives.Components;
using Content.Shared.Objectives.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Objectives.Systems;

public abstract partial class SharedObjectivesSystem
{
    /// <summary>
    /// Sets the organisation that issued the objective.
    /// </summary>
    public void SetIssuer(EntityUid uid, ProtoId<ObjectiveIssuerPrototype> issuer, ObjectiveComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return;

        comp.Issuer = issuer;
    }
}
