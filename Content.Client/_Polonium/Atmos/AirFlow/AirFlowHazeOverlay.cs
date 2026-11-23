using System.Numerics;
using Content.Client.Graphics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using static Content.Client._Polonium.Atmos.AirFlow.AirFlowVisualsSystem;

namespace Content.Client._Polonium.Atmos.AirFlow;

public sealed partial class AirFlowHazeOverlay : Overlay
{
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IResourceCache _resourceCache = default!;

    private static readonly ProtoId<ShaderPrototype> UnshadedShader = "unshaded";
    private static readonly ProtoId<ShaderPrototype> HazeShader = "PoloniumAirFlowHaze";
    private static readonly Color EmptyColor = new(0, 0, 0, 0);
    private static readonly ResPath NoiseTexture = new("/Textures/Effects/HeatBlur/perlin_noise.png");
    private static readonly ResPath BlobTexture = new("/Textures/Effects/HeatBlur/soft_circle.png");

    private readonly AirFlowVisualsSystem _system;
    private readonly SharedTransformSystem _xform;
    private readonly ShaderInstance _shader;
    private readonly Texture _noise;
    private readonly Texture _blob;
    private readonly OverlayResourceCache<CachedResources> _resources = new();

    public override bool RequestScreenTexture { get; set; } = true;
    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public AirFlowHazeOverlay(AirFlowVisualsSystem system)
    {
        IoCManager.InjectDependencies(this);
        _system = system;
        _xform = _entMan.System<SharedTransformSystem>();
        _shader = _proto.Index(HazeShader).InstanceUnique();
        _noise = _resourceCache.GetResource<TextureResource>(NoiseTexture).Texture;
        _blob = _resourceCache.GetResource<TextureResource>(BlobTexture).Texture;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (args.MapId == MapId.Nullspace || _system.HazeCount == 0)
            return false;

        var res = _resources.GetForViewport(args.Viewport, static _ => new CachedResources());
        var target = args.Viewport.RenderTarget;

        if (res.Target?.Texture.Size != target.Size)
        {
            res.Target?.Dispose();
            res.Target = _clyde.CreateRenderTarget(
                target.Size,
                new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb),
                name: nameof(AirFlowHazeOverlay));
        }

        var handle = args.WorldHandle;
        var mapId = args.MapId;
        var worldBounds = args.WorldBounds;
        var worldToViewportLocal = args.Viewport.GetWorldToLocalMatrix();
        var particles = _system.Particles;
        var count = _system.ParticleCount;
        var any = false;

        handle.UseShader(_proto.Index(UnshadedShader).Instance());

        // The strength of the distortion goes into the red channel of the target, the shader reads it back.
        handle.RenderInRenderTarget(res.Target,
            () =>
            {
                var grid = EntityUid.Invalid;
                var visible = false;
                var bounds = default(Box2);
                var gridFade = 0f;

                for (var i = 0; i < count; i++)
                {
                    ref readonly var particle = ref particles[i];
                    if (particle.Kind != ParticleKind.Haze)
                        continue;

                    if (particle.Grid != grid)
                    {
                        grid = particle.Grid;
                        gridFade = _system.GetFade(grid);
                        visible = _entMan.TryGetComponent(grid, out TransformComponent? xform) && xform.MapID == mapId;
                        if (visible)
                        {
                            var (_, _, matrix, invMatrix) = _xform.GetWorldPositionRotationMatrixWithInv(grid);
                            handle.SetTransform(matrix * worldToViewportLocal);
                            bounds = invMatrix.TransformBox(worldBounds).Enlarged(2f);
                        }
                    }

                    if (!visible || !bounds.Contains(particle.Pos))
                        continue;

                    var progress = particle.Age / particle.Life;
                    var strength = particle.Alpha * gridFade * MathF.Min(1f, progress * 4f) * MathF.Min(1f, (1f - progress) * 3f);
                    if (particle.Out > 0f)
                        strength *= MathF.Max(0f, 1f - particle.Out / GetOutLife(ParticleKind.Haze));

                    if (strength <= 0f)
                        continue;

                    any = true;
                    handle.DrawTextureRect(_blob,
                        Box2.CenteredAround(particle.Pos, new Vector2(particle.Size * 2f)),
                        new Color(strength, 0f, 0f));
                }
            },
            EmptyColor);

        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
        return any;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var res = _resources.GetForViewport(args.Viewport, static _ => new CachedResources());

        if (ScreenTexture == null || res.Target == null)
            return;

        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shader.SetParameter("NOISE_TEXTURE", _noise);

        args.WorldHandle.UseShader(_shader);
        args.WorldHandle.DrawTextureRect(res.Target.Texture, args.WorldBounds);
        args.WorldHandle.UseShader(null);
    }

    protected override void DisposeBehavior()
    {
        _resources.Dispose();
        base.DisposeBehavior();
    }

    private sealed class CachedResources : IDisposable
    {
        public IRenderTexture? Target;

        public void Dispose()
        {
            Target?.Dispose();
        }
    }
}
