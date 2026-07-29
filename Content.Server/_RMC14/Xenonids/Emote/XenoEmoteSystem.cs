using Content.Server.Chat.Systems;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Chat;
using Robust.Shared.Prototypes;

namespace Content.Server._RMC14.Xenonids.Emote;

public sealed partial class XenoEmoteSystem : EntitySystem
{
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoComponent, ComponentStartup>(OnXenoStartup);
        SubscribeLocalEvent<XenoComponent, EmoteEvent>(OnXenoEmote);
    }

    private void OnXenoStartup(Entity<XenoComponent> xeno, ref ComponentStartup args)
    {
        if (xeno.Comp.EmoteSounds == null)
            return;

        _proto.TryIndex(xeno.Comp.EmoteSounds, out xeno.Comp.Sounds);
    }

    private void OnXenoEmote(Entity<XenoComponent> xeno, ref EmoteEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = _chat.TryPlayEmoteSound(xeno, xeno.Comp.Sounds, args.Emote);
    }
}
