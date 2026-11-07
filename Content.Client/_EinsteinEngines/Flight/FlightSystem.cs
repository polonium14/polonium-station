using Content.Shared._EinsteinEngines.Flight;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Client._EinsteinEngines.Flight;

public sealed partial class FlightSystem : SharedFlightSystem
{
    [Dependency] private IPrototypeManager _prototype = default!;

    private static readonly ProtoId<ShaderPrototype> FlapShader = "Flap";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FlightComponent, AfterAutoHandleStateEvent>(OnHandleState);
    }

    private void OnHandleState(Entity<FlightComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (ent.Comp.AnimatedRsi is not { } rsi || !TryComp<SpriteComponent>(ent, out var sprite))
            return;

        ShaderInstance? shader = null;
        if (ent.Comp.On)
        {
            shader = _prototype.Index(FlapShader).InstanceUnique();
            shader.SetParameter("Speed", ent.Comp.ShaderSpeed);
            shader.SetParameter("Offset", ent.Comp.ShaderOffset);
            shader.SetParameter("Multiplier", ent.Comp.ShaderMultiplier);
        }

        var index = 0;
        foreach (var layer in sprite.AllLayers)
        {
            if (layer.Rsi?.Path == rsi)
                sprite.LayerSetShader(index, shader);

            index++;
        }
    }
}
