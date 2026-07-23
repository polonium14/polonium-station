using Content.Shared.Body;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Speech.Muting;
using Robust.Shared.Timing;

namespace Content.Shared._Shitmed.Medical;

public sealed partial class OrganFunctionSystem : EntitySystem
{
    [Dependency] private BlindableSystem _blindable = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OrganComponent, OrganGotRemovedEvent>(OnOrganRemoved);
        SubscribeLocalEvent<OrganComponent, OrganGotInsertedEvent>(OnOrganInserted);
    }

    private void OnOrganRemoved(Entity<OrganComponent> ent, ref OrganGotRemovedEvent args)
    {
        // These handlers cascade into adding/removing networked components (BlurryVision,
        // Muted) on the body. Doing that while the client is applying a server game state
        // mutates MetaDataComponent.NetComponents mid-iteration in ResetPredictedEntities
        // and crashes the client - and the authoritative components arrive with the same
        // state anyway, so deriving them here is pure misprediction.
        if (_timing.ApplyingState)
            return;

        // Organ removal also fires as part of a mob's own teardown/deletion cascade, not just
        // "surgically remove one organ from an otherwise-alive body" - touching a terminating
        // entity here (SetMinDamage raises EyeDamageChangedEvent, which tries to AddComponent
        // a BlurryVisionComponent on it) crashes with "attempted to add a component to an
        // entity while it is terminating." Same bug class as the tourniquet organ-removal fix
        // earlier this session - skip entirely when the body is on its way out.
        if (TerminatingOrDeleted(args.Target))
            return;

        switch (ent.Comp.Category?.Id)
        {
            case "Eyes":
                if (TryComp<BlindableComponent>(args.Target, out var blindable))
                    _blindable.SetMinDamage((args.Target, blindable), blindable.MaxDamage);
                break;

            case "Tongue":
                EnsureComp<MutedComponent>(args.Target);
                break;
        }
    }

    private void OnOrganInserted(Entity<OrganComponent> ent, ref OrganGotInsertedEvent args)
    {
        if (_timing.ApplyingState)
            return;

        if (TerminatingOrDeleted(args.Target))
            return;

        switch (ent.Comp.Category?.Id)
        {
            case "Eyes":
                if (TryComp<BlindableComponent>(args.Target, out var blindable))
                {
                    _blindable.SetMinDamage((args.Target, blindable), 0);
                    _blindable.AdjustEyeDamage((args.Target, blindable), -blindable.EyeDamage);
                }

                break;

            case "Tongue":
                RemComp<MutedComponent>(args.Target);
                break;
        }
    }
}
