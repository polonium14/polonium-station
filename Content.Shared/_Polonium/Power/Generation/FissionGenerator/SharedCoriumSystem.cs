using Content.Shared.Damage.Systems;

namespace Content.Shared._Polonium.Power.Generation.FissionGenerator;

public abstract partial class SharedCoriumSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CoriumComponent, BeforeDamageChangedEvent>(OnBeforeDamageChanged);
    }

    private void OnBeforeDamageChanged(Entity<CoriumComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (!ent.Comp.Stages.TryGetValue(ent.Comp.Stage, out var stage) || !stage.Breakable)
            args.Cancelled = true;
    }
}
