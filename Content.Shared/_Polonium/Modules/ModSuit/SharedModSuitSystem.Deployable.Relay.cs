using Content.Shared._Polonium.Modules.Events;
using Content.Shared._Polonium.Modules.ModSuit.Components;
using Content.Shared._Polonium.Modules.ModSuit.Events;
using Content.Shared.PowerCell;
using JetBrains.Annotations;

namespace Content.Shared._Polonium.Modules.ModSuit;

public partial class SharedModSuitSystem
{
    private void InitializeDeployableRelay()
    {
        SubscribeLocalEvent<ModSuitPartDeployableComponent, ModSuitGetUiEntriesEvent>(RelayToAllParts);
        SubscribeLocalEvent<ModSuitPartDeployableComponent, ModuleContainerGetBasePowerDrawRate>(RelayToAllPartsAndSuit);
        SubscribeLocalEvent<ModSuitPartDeployableComponent, PowerCellSlotEmptyEvent>(RelayToDeployedPartsAndSuit);

        SubscribeLocalEvent<ModSuitDeployedPartComponent, ModSuitSealAttemptEvent>(RelayToSuit);
    }

    protected void RelayToDeployedParts<T>(Entity<ModSuitPartDeployableComponent> ent, ref T args)
    {
        var partNumber = 0;
        foreach (var part in GetDeployedParts(ent!))
        {
            var ev = new ModSuitDeployableRelayedEvent<T>(args, ent.Owner, partNumber++);
            RaiseLocalEvent(part, ref ev);
            args = ev.Args;
        }
    }

    protected void RelayToDeployedPartsAndSuit<T>(Entity<ModSuitPartDeployableComponent> ent, ref T args)
    {
        var partNumber = 0;

        var suitEv = new ModSuitDeployableRelayedEvent<T>(args, ent.Owner, partNumber++);
        RaiseLocalEvent(ent.Owner, ref suitEv);
        args = suitEv.Args;

        foreach (var part in GetDeployedParts(ent!))
        {
            var partEv = new ModSuitDeployableRelayedEvent<T>(args, ent.Owner, partNumber++);
            RaiseLocalEvent(part, ref partEv);
            args = partEv.Args;
        }
    }

    protected void RelayToDeployableParts<T>(Entity<ModSuitPartDeployableComponent> ent, ref T args)
    {
        var partNumber = 0;
        foreach (var part in GetDeployedParts(ent!))
        {
            var ev = new ModSuitDeployableRelayedEvent<T>(args, ent.Owner, partNumber++);
            RaiseLocalEvent(part, ref ev);
            args = ev.Args;
        }
    }

    protected void RelayToDeployablePartsAndSuit<T>(Entity<ModSuitPartDeployableComponent> ent, ref T args)
    {
        var partNumber = 0;

        var suitEv = new ModSuitDeployableRelayedEvent<T>(args, ent.Owner, partNumber++);
        RaiseLocalEvent(ent.Owner, ref suitEv);
        args = suitEv.Args;

        foreach (var part in GetDeployedParts(ent!))
        {
            var partEv = new ModSuitDeployableRelayedEvent<T>(args, ent.Owner, partNumber++);
            RaiseLocalEvent(part, ref partEv);
            args = partEv.Args;
        }
    }

    protected void RelayToAllParts<T>(Entity<ModSuitPartDeployableComponent> ent, ref T args)
    {
        var partNumber = 0;
        foreach (var part in GetAllParts(ent!))
        {
            var ev = new ModSuitDeployableRelayedEvent<T>(args, ent.Owner, partNumber++);
            RaiseLocalEvent(part, ref ev);
            args = ev.Args;
        }
    }

    protected void RelayToAllPartsAndSuit<T>(Entity<ModSuitPartDeployableComponent> ent, ref T args)
    {
        var partNumber = 0;

        var suitEv = new ModSuitDeployableRelayedEvent<T>(args, ent.Owner, partNumber++);
        RaiseLocalEvent(ent.Owner, ref suitEv);
        args = suitEv.Args;

        foreach (var part in GetAllParts(ent!))
        {
            var partEv = new ModSuitDeployableRelayedEvent<T>(args, ent.Owner, partNumber++);
            RaiseLocalEvent(part, ref partEv);
            args = partEv.Args;
        }
    }

    protected void RelayToSuit<T>(Entity<ModSuitDeployedPartComponent> ent, ref T args)
    {
        var ev = new ModSuitDeployedPartRelayedEvent<T>(args, ent.Owner);
        RaiseEventToSuit((ent.Owner, ent.Comp), ref ev);
    }

    [PublicAPI]
    public void RaiseEventToDeployedParts<T>(Entity<ModSuitPartDeployableComponent?> ent, T ev) where T : class
    {
        foreach (var part in GetDeployedParts(ent))
            RaiseLocalEvent(part, ev);
    }

    [PublicAPI]
    public void RaiseEventToDeployedParts<T>(Entity<ModSuitPartDeployableComponent?> ent, ref T ev) where T : struct
    {
        foreach (var part in GetDeployedParts(ent))
            RaiseLocalEvent(part, ref ev);
    }

    [PublicAPI]
    public void RaiseEventToDeployableParts<T>(Entity<ModSuitPartDeployableComponent?> ent, T ev) where T : class
    {
        foreach (var part in GetDeployableParts(ent))
            RaiseLocalEvent(part, ev);
    }

    [PublicAPI]
    public void RaiseEventToDeployableParts<T>(Entity<ModSuitPartDeployableComponent?> ent, ref T ev) where T : struct
    {
        foreach (var part in GetDeployableParts(ent))
            RaiseLocalEvent(part, ref ev);
    }

    [PublicAPI]
    public void RaiseEventToAllParts<T>(Entity<ModSuitPartDeployableComponent?> ent, T ev) where T : class
    {
        foreach (var part in GetAllParts(ent))
            RaiseLocalEvent(part, ref ev);
    }

    [PublicAPI]
    public void RaiseEventToAllParts<T>(Entity<ModSuitPartDeployableComponent?> ent, ref T ev) where T : struct
    {
        foreach (var part in GetAllParts(ent))
            RaiseLocalEvent(part, ref ev);
    }

    [PublicAPI]
    public void RaiseEventToSuit<T>(Entity<ModSuitDeployedPartComponent?> ent, T ev) where T : class
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
            return;

        RaiseLocalEvent(ent.Comp.Suit, ref ev);
    }

    [PublicAPI]
    public void RaiseEventToSuit<T>(Entity<ModSuitDeployedPartComponent?> ent, ref T ev) where T : struct
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
            return;

        RaiseLocalEvent(ent.Comp.Suit, ref ev);
    }
}
