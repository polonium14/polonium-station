using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Robust.Shared.GameObjects;

namespace Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;

/// <summary>
/// A TraumaComponent's TraumaTarget/HoldingWoundable fields (both AutoNetworkedField
/// EntityUid?) reference whatever entity a trauma is "about" - a vital organ (OrganDamage), a
/// bone (BoneDamage), or the woundable itself (NerveDamage/HoldingWoundable). None of those
/// referenced entities live inside the trauma's own container hierarchy (the trauma is
/// contained in its wound's TraumaContainer, which is contained in the WOUNDABLE that got hit -
/// a completely separate entity from whatever it's targeting, especially for OrganDamage, whose
/// target can be any vital organ anywhere on the body, not just ones "inside" the wounded limb).
/// So deleting a referenced entity never cascade-deletes the trauma that references it, and
/// every one-off cleanup added at a specific removal call site (organ destroyed by damage,
/// organ surgically removed, limb dismembered) only covers that one path - the next new way to
/// remove/delete an organ, bone, or limb reintroduces the same "Can't resolve MetaDataComponent"
/// PVS-serialization crash on a dangling reference.
///
/// Fixed generically instead: whenever any organ/bone/woundable actually terminates (regardless
/// of what caused it - damage destruction, surgery, dismemberment, admin deletion, or anything
/// else not yet written), sweep every trauma and drop the ones that reference it. This replaces
/// the bespoke cleanup that used to live in TraumaSystem.Organs.cs's OnOrganSeverityChanged,
/// SharedSurgerySystem.Steps.cs's OnRemoveOrganStep, and DismembermentSystem.Dismember.
/// </summary>
public partial class TraumaSystem
{
    private void InitializeCleanup()
    {
        SubscribeLocalEvent<OrganIntegrityComponent, EntityTerminatingEvent>(OnTraumaTargetTerminating);
        SubscribeLocalEvent<BoneComponent, EntityTerminatingEvent>(OnTraumaTargetTerminating);
        SubscribeLocalEvent<WoundableComponent, EntityTerminatingEvent>(OnTraumaTargetTerminating);
    }

    private void OnTraumaTargetTerminating<T>(EntityUid uid, T component, ref EntityTerminatingEvent args) where T : IComponent
    {
        var query = EntityQueryEnumerator<TraumaComponent>();
        while (query.MoveNext(out var traumaEnt, out var traumaComp))
        {
            // The trauma (or its holding wound) can itself be mid-termination as part of the
            // very same deletion cascade that triggered this handler (e.g. a whole body being
            // gibbed) - skip it rather than trying to mutate a container/raise events on
            // something already tearing down.
            if (TerminatingOrDeleted(traumaEnt))
                continue;

            if (traumaComp.TraumaTarget == uid || traumaComp.HoldingWoundable == uid)
                RemoveTrauma((traumaEnt, traumaComp));
        }
    }
}
