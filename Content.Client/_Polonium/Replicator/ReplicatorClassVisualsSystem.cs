using Content.Client._Impstation.Replicator;
using Content.Client.DamageState;
using Content.Shared._Impstation.Replicator;
using Content.Shared._Polonium.Replicator;
using Content.Shared.Mobs;
using Robust.Client.GameObjects;

namespace Content.Client._Polonium.Replicator;

public sealed partial class ReplicatorClassVisualsSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private ReplicatorModuleSystem _modules = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReplicatorModulesComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ReplicatorModulesComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<ReplicatorModulesComponent, AppearanceChangeEvent>(OnAppearanceChange,
            after: [typeof(DamageStateVisualizerSystem), typeof(ReplicatorVisualsSystem)]);
    }

    private void OnStartup(Entity<ReplicatorModulesComponent> ent, ref ComponentStartup args)
    {
        UpdateForm(ent);
    }

    private void OnState(Entity<ReplicatorModulesComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateForm(ent);
    }

    private void OnAppearanceChange(Entity<ReplicatorModulesComponent> ent, ref AppearanceChangeEvent args)
    {
        UpdateForm(ent);
    }

    private void UpdateForm(Entity<ReplicatorModulesComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        var dead = _appearance.TryGetData<MobState>(ent, MobStateVisuals.State, out var state) && state == MobState.Dead;
        var hasThrust = _sprite.LayerMapTryGet((ent.Owner, sprite), ReplicatorVisuals.Thrust, out var thrust, false);

        if (hasThrust)
            _sprite.LayerSetVisible((ent.Owner, sprite), thrust, !dead);

        if (!_modules.TryGetClass(ent.AsNullable(), out var prototype)
            || prototype.GetForm(_modules.GetTotalStages(ent.Comp)) is not { } form)
            return;

        if (TryComp<DamageStateVisualsComponent>(ent, out var damageState))
        {
            SetState(damageState, MobState.Alive, form.State);
            SetState(damageState, MobState.Dead, form.DeadState);
        }

        if (_sprite.LayerMapTryGet((ent.Owner, sprite), DamageStateVisualLayers.Base, out var body, false))
            _sprite.LayerSetRsi((ent.Owner, sprite), body, prototype.Sprite, dead ? form.DeadState : form.State);

        if (hasThrust)
            _sprite.LayerSetRsi((ent.Owner, sprite), thrust, prototype.Sprite, form.ThrustState);

        if (!_sprite.LayerMapTryGet((ent.Owner, sprite), ReplicatorVisuals.Combat, out var combat, false))
            return;

        _sprite.LayerSetRsi((ent.Owner, sprite), combat, prototype.Sprite, form.CombatState);

        if (dead)
            _sprite.LayerSetVisible((ent.Owner, sprite), combat, false);
    }

    private static void SetState(DamageStateVisualsComponent component, MobState state, string value)
    {
        if (!component.States.TryGetValue(state, out var layers))
            component.States[state] = layers = new Dictionary<DamageStateVisualLayers, string>();

        layers[DamageStateVisualLayers.Base] = value;
    }
}
