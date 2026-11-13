using Content.Client.IconSmoothing;
using Content.Shared._Polonium.Power.Generation.FissionGenerator;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._Polonium.Power.Generation.FissionGenerator;

public sealed partial class CoriumSystem : SharedCoriumSystem
{
    [Dependency] private IconSmoothSystem _iconSmooth = default!;

    // Order of the smoothing states in the corium prototype.
    private const int BaseState = 0;
    private const int GlowState = 1;
    private const string GlowSuffix = "_glow";

    private static readonly ProtoId<ShaderPrototype> GlowShader = "unshaded";

    // CornerSpriteSmoothing names its layers after the corner, but applies its own shader to the bare layer key,
    // which doesn't exist. So the glow corners get their shader here instead of from the prototype.
    private static readonly string[] GlowCorners =
    [
        "glow" + Direction.SouthEast,
        "glow" + Direction.SouthWest,
        "glow" + Direction.NorthEast,
        "glow" + Direction.NorthWest,
    ];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CoriumComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CoriumComponent, AfterAutoHandleStateEvent>(OnHandleState);
    }

    private void OnStartup(Entity<CoriumComponent> ent, ref ComponentStartup args)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite))
        {
            foreach (var corner in GlowCorners)
            {
                sprite.LayerSetShader(corner, GlowShader.Id);
            }
        }

        UpdateSprite(ent);
    }

    private void OnHandleState(Entity<CoriumComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateSprite(ent);
    }

    private void UpdateSprite(Entity<CoriumComponent> ent)
    {
        if (!ent.Comp.Stages.TryGetValue(ent.Comp.Stage, out var stage))
            return;

        _iconSmooth.SetStateBase(ent.Owner, BaseState, stage.State);
        _iconSmooth.SetStateBase(ent.Owner, GlowState, stage.State + GlowSuffix);
    }
}
