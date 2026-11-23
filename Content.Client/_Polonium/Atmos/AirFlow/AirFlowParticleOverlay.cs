using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Utility;
using static Content.Client._Polonium.Atmos.AirFlow.AirFlowVisualsSystem;

namespace Content.Client._Polonium.Atmos.AirFlow;

public sealed partial class AirFlowParticleOverlay : Overlay
{
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IResourceCache _resources = default!;

    private static readonly ResPath PuffTexture = new("/Textures/Effects/HeatBlur/soft_circle.png");
    private static readonly Color DustColor = Color.FromHex("#d8d0be");
    private static readonly Color GrainColor = Color.FromHex("#4d4a45");

    private const float Pixel = 1f / EyeManager.PixelsPerMeter;

    private readonly AirFlowVisualsSystem _system;
    private readonly SharedTransformSystem _xform;
    private readonly Texture _puff;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;

    public AirFlowParticleOverlay(AirFlowVisualsSystem system)
    {
        IoCManager.InjectDependencies(this);
        _system = system;
        _xform = _entMan.System<SharedTransformSystem>();
        _puff = _resources.GetResource<TextureResource>(PuffTexture).Texture;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return _system.ParticleCount > _system.HazeCount;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var particles = _system.Particles;
        var count = _system.ParticleCount;

        var grid = EntityUid.Invalid;
        var visible = false;
        var bounds = default(Box2);
        var gridFade = 0f;

        for (var i = 0; i < count; i++)
        {
            ref readonly var particle = ref particles[i];
            if (particle.Kind == ParticleKind.Haze)
                continue;

            if (particle.Grid != grid)
            {
                grid = particle.Grid;
                gridFade = _system.GetFade(grid);
                visible = _entMan.TryGetComponent(grid, out TransformComponent? xform) && xform.MapID == args.MapId;
                if (visible)
                {
                    var (_, _, matrix, invMatrix) = _xform.GetWorldPositionRotationMatrixWithInv(grid);
                    handle.SetTransform(matrix);
                    bounds = invMatrix.TransformBox(args.WorldBounds).Enlarged(1f);
                }
            }

            if (!visible || !bounds.Contains(particle.Pos))
                continue;

            var progress = particle.Age / particle.Life;
            var speed = particle.Vel.Length();

            switch (particle.Kind)
            {
                case ParticleKind.Fog:
                {
                    var fade = MathF.Min(1f, progress * 5f) * (1f - progress) * gridFade;
                    var size = particle.Size * (1f + progress * 0.7f);
                    if (particle.Out > 0f)
                    {
                        fade *= MathF.Max(0f, 1f - particle.Out * 0.9f);
                        size *= 1f + particle.Out * 1.5f;
                    }

                    var alpha = Math.Clamp(particle.Alpha * fade * 2.2f, 0f, 1f);
                    handle.DrawTextureRect(_puff,
                        Box2.CenteredAround(particle.Pos, new Vector2(size * 2f)),
                        particle.Color.WithAlpha(alpha));
                    break;
                }
                case ParticleKind.Dust:
                case ParticleKind.Grain:
                {
                    var fade = MathF.Min(1f, particle.Age * 6f)
                               * MathF.Min(1f, (1f - progress) * 4f)
                               * Math.Clamp(speed / 0.5f, 0f, 1f)
                               * gridFade;
                    if (particle.Out > 0f)
                        fade *= MathF.Max(0f, 1f - particle.Out / GetOutLife(particle.Kind));

                    if (fade < 0.03f)
                        break;

                    var grain = particle.Kind == ParticleKind.Grain;
                    var size = grain ? Pixel * 2f : Pixel;
                    var corner = new Vector2(MathF.Round(particle.Pos.X / Pixel) * Pixel, MathF.Round(particle.Pos.Y / Pixel) * Pixel);
                    handle.DrawRect(new Box2(corner, corner + new Vector2(size)),
                        (grain ? GrainColor : DustColor).WithAlpha(0.9f * fade));
                    break;
                }
                case ParticleKind.Wisp:
                {
                    if (speed < 0.25f)
                        break;

                    var fade = MathF.Min(1f, progress * 6f) * MathF.Min(1f, (1f - progress) * 3f) * gridFade;
                    if (particle.Out > 0f)
                        fade *= MathF.Max(0f, 1f - particle.Out / GetOutLife(ParticleKind.Wisp));

                    var alpha = fade * Math.Clamp(speed / 3f, 0.32f, 1f) * 0.95f;
                    if (alpha < 0.02f)
                        break;

                    var length = Math.Clamp(speed * 0.14f, 0.18f, 0.9f);
                    var box = new Box2(particle.Pos.X - length, particle.Pos.Y - Pixel / 2f, particle.Pos.X, particle.Pos.Y + Pixel / 2f);
                    handle.DrawRect(new Box2Rotated(box, new Angle(particle.Vel), particle.Pos), particle.Color.WithAlpha(alpha));
                    break;
                }
            }
        }

        handle.SetTransform(Matrix3x2.Identity);
    }
}
