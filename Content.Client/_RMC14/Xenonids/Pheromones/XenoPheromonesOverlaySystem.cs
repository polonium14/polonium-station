using Robust.Client.Graphics;

namespace Content.Client._RMC14.Xenonids.Pheromones;

public sealed partial class XenoPheromonesOverlaySystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlay = default!;

    public override void Initialize()
    {
        if (!_overlay.HasOverlay<XenoPheromonesOverlay>())
            _overlay.AddOverlay(new XenoPheromonesOverlay());
    }

    public override void Shutdown()
    {
        _overlay.RemoveOverlay<XenoPheromonesOverlay>();
    }
}
