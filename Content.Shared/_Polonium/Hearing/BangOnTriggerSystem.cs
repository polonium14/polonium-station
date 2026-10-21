using Content.Shared.EntityEffects;
using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.Screech;
using Content.Shared.StatusEffect;
using Content.Shared.Trigger;
using Content.Shared.Trigger.Systems;

namespace Content.Shared._Polonium.Hearing;

public sealed partial class BangOnTriggerSystem : XOnTriggerSystem<BangOnTriggerComponent>
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private SharedEntityEffectsSystem _effects = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    [Dependency] private EntityQuery<StatusEffectsComponent> _statusEffectsQuery = default!;

    private readonly HashSet<EntityUid> _entSet = [];

    protected override void OnTrigger(Entity<BangOnTriggerComponent> ent, EntityUid target, ref TriggerEvent args)
    {
        var comp = ent.Comp;

        _entSet.Clear();
        _lookup.GetEntitiesInRange(Transform(target).Coordinates, comp.Range, _entSet);

        foreach (var entity in _entSet)
        {
            // same filter as the changeling screech: only things with status effects can be stunned by noise
            if (!_statusEffectsQuery.HasComponent(entity))
                continue;

            if (comp.RequireUnoccluded && !_examine.InRangeUnOccluded(target, entity, comp.Range))
                continue;

            var ev = new ScreechEffectAttemptEvent(target);
            RaiseLocalEvent(entity, ref ev);

            if (ev.Cancelled)
            {
                if (comp.ProtectedPopup is { } protectedPopup)
                    _popup.PopupEntity(Loc.GetString(protectedPopup), entity, entity);
                continue;
            }

            foreach (var effect in comp.Effects)
            {
                _effects.TryApplyEffect(entity, effect, user: args.User);
            }

            if (comp.HitPopup is { } hitPopup)
                _popup.PopupEntity(Loc.GetString(hitPopup), entity, entity, PopupType.MediumCaution);
        }

        args.Handled = true;
    }
}
