using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Atmos.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Clothing;
using Content.Shared.Examine;
using Content.Shared._Polonium.Modules.Events;
using Content.Shared._Polonium.Modules.ModSuit.Components;
using Content.Shared._Polonium.Modules.ModSuit.Events;
using Content.Shared._Polonium.Modules.ModSuit.UI;
using Content.Shared.PowerCell;
using JetBrains.Annotations;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Utility;

namespace Content.Shared._Polonium.Modules.ModSuit;

public partial class SharedModSuitSystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedInternalsSystem _internals = default!;

    private const int MaxSealButtonParts = 8;

    private void InitializeSealable()
    {
        SubscribeLocalEvent<ModSuitSealableComponent, ComponentInit>(OnSealableInit);

        SubscribeLocalEvent<ModSuitSealableComponent, ClothingGotEquippedEvent>(OnSealableEquipped);
        SubscribeLocalEvent<ModSuitSealableComponent, ClothingGotUnequippedEvent>(OnSealableUnequipped);

        SubscribeLocalEvent<ModSuitSealableComponent, ModSuitDeployablePartUndeployedEvent>(OnSealableDeployablePartUndeployed);

        SubscribeLocalEvent<ModSuitSealableComponent, ExaminedEvent>(OnSealableExamined);

        SubscribeLocalEvent<ModSuitSealableComponent, ModuleGetUserEvent>(OnSealableGetUser);

        SubscribeLocalEvent<ModSuitSealableComponent, ModSuitGetUiEntriesEvent>(OnSealableGetUiEntries);

        SubscribeLocalEvent<BreathToolComponent, ModSuitSealedEvent>(OnBreathToolSealed);
        SubscribeLocalEvent<ModSuitSealableComponent, ModSuitDeployableRelayedEvent<ModSuitGetUiEntriesEvent>>((u, c, a) => OnSealableGetUiEntries((u, c), ref a.Args));

        SubscribeLocalEvent<ModSuitSealablePowerDrawComponent, ModSuitDeployableRelayedEvent<ModuleContainerGetBasePowerDrawRate>>(OnSealablePowerGetPowerDraw);
        SubscribeLocalEvent<ModSuitSealablePowerDrawComponent, ModSuitSealedEvent>(OnSealablePowerSealed);
        SubscribeLocalEvent<ModSuitSealablePowerDrawComponent, ModSuitUnsealedEvent>(OnSealablePowerUnsealed);

        SubscribeLocalEvent<ToggleableComponentModSuitPartDeployableAllSealedComponent, ModSuitContainerPartSealedEvent>(OnToggleComponentSealed);
        SubscribeLocalEvent<ToggleableComponentModSuitPartDeployableAllSealedComponent, ModSuitContainerPartUnsealedEvent>(OnToggleComponentUnsealed);

        SubscribeLocalEvent<ModSuitUserInterfaceComponent, ModSuitSealButtonMessage>(OnSealableUiButton);
    }

    private void UpdateSealable(float frameTime)
    {
        var query = EntityQueryEnumerator<ModSuitSealableComponent, ModSuitSealablePendingComponent>();
        while (query.MoveNext(out var uid, out var sealable, out var pending))
        {
            if (pending.NextUpdate > _timing.CurTime)
                continue;

            SetSeal((uid, sealable), pending.ShouldSeal);
            RemCompDeferred<ModSuitSealablePendingComponent>(uid);
        }
    }

    private void OnSealableInit(Entity<ModSuitSealableComponent> ent, ref ComponentInit args)
    {
        Appearance.SetData(ent.Owner, ModSuitSealedVisuals.Sealed, ent.Comp.Sealed);
    }

    private void OnSealableEquipped(Entity<ModSuitSealableComponent> ent, ref ClothingGotEquippedEvent args)
    {
        ent.Comp.Wearer = args.Wearer;
        Dirty(ent);
    }

    private void OnSealableUnequipped(Entity<ModSuitSealableComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        // handled in a separate event
        if (!HasComp<ModSuitDeployedPartComponent>(ent.Owner))
        {
            // cant be sealed when not worn
            SetSeal((ent.Owner, ent.Comp), false);
        }

        ent.Comp.Wearer = null;
        Dirty(ent);
    }

    private void OnSealableDeployablePartUndeployed(Entity<ModSuitSealableComponent> ent, ref ModSuitDeployablePartUndeployedEvent args)
    {
        if (!TerminatingOrDeleted(ent.Owner))
            SetSeal((ent.Owner, ent.Comp), false, args.PartNumber);
    }

    private void OnBreathToolSealed(Entity<BreathToolComponent> ent, ref ModSuitSealedEvent args)
    {
        if (TryComp<InternalsComponent>(args.Wearer, out var internals))
            _internals.ConnectBreathTool((args.Wearer.Value, internals), ent.Owner);
    }

    private void OnSealableExamined(Entity<ModSuitSealableComponent> ent, ref ExaminedEvent args)
    {
        var msg = ent.Comp.Sealed ? Loc.GetString("modsuit-sealable-examine-sealed") : Loc.GetString("modsuit-sealable-examine-unsealed");
        args.PushMarkup(msg);
    }

    private void OnSealableGetUser(Entity<ModSuitSealableComponent> ent, ref ModuleGetUserEvent args)
    {
        args.User ??= ent.Comp.Wearer;
    }

    private void OnSealableGetUiEntries(Entity<ModSuitSealableComponent> ent, ref ModSuitGetUiEntriesEvent args)
    {
        var type = CompOrNull<ModSuitPartTypeComponent>(ent.Owner)?.Type ?? ModSuitPartType.Other;
        var newData = new ModSuitSealablePartBuiEntry(GetUiLayer(ent), type, ent.Comp.Sealed);
        var toAdd = KeyValuePair.Create(GetNetEntity(ent.Owner), newData);

        ModSuitSealableBuiEntry? foundEntry = null;

        // find any state that another part may have added
        foreach (var entry in args.Entries)
        {
            if (entry is not ModSuitSealableBuiEntry sealableEntry)
                continue;

            foundEntry = sealableEntry;
            break;
        }

        var length = (foundEntry?.Parts.Length ?? 0) + 1;
        var parts = new KeyValuePair<NetEntity, ModSuitSealablePartBuiEntry>[length];

        if (foundEntry == null)
        {
            parts = [toAdd];
            var newEntry = new ModSuitSealableBuiEntry(parts);
            args.Entries.Add(newEntry);
            return;
        }

        var insertIndex = foundEntry.Parts.Length;
        for (var i = 0; i < foundEntry.Parts.Length; i++)
        {
            if (foundEntry.Parts[i].Value.Type > type)
            {
                insertIndex = i;
                break;
            }
        }

        if (insertIndex > 0)
            Array.Copy(foundEntry.Parts, parts, insertIndex);

        parts[insertIndex] = toAdd;

        if (insertIndex < foundEntry.Parts.Length)
            Array.Copy(foundEntry.Parts, insertIndex, parts, insertIndex + 1, foundEntry.Parts.Length - insertIndex);

        foundEntry.Parts = parts;
    }

    private SpriteSpecifier GetUiLayer(Entity<ModSuitSealableComponent> ent)
    {
        if (IsUnpowered(ent.Owner) && ent.Comp.UnpoweredUiLayer.TryGetValue(ent.Comp.Sealed, out var unpowered))
            return unpowered;

        return ent.Comp.UiLayer.GetValueOrDefault(ent.Comp.Sealed, SpriteSpecifier.Invalid);
    }

    private void OnSealablePowerGetPowerDraw(Entity<ModSuitSealablePowerDrawComponent> ent, ref ModSuitDeployableRelayedEvent<ModuleContainerGetBasePowerDrawRate> args)
    {
        var isSealed = IsSealed(ent.Owner);
        if (ent.Comp.PowerDraw.TryGetValue(isSealed, out var powerDraw))
            args.Args.BaseRate += powerDraw;
    }

    private void OnSealablePowerSealed(Entity<ModSuitSealablePowerDrawComponent> ent, ref ModSuitSealedEvent args)
    {
        if (TryComp<ModSuitDeployedPartComponent>(ent.Owner, out var deployedPart))
            _module.UpdatePowerDraw(deployedPart.Suit);
        else if (HasComp<ModSuitPartDeployableComponent>(ent.Owner))
            _module.UpdatePowerDraw(ent.Owner);
    }

    private void OnSealablePowerUnsealed(Entity<ModSuitSealablePowerDrawComponent> ent, ref ModSuitUnsealedEvent args)
    {
        if (TryComp<ModSuitDeployedPartComponent>(ent.Owner, out var deployedPart))
            _module.UpdatePowerDraw(deployedPart.Suit);
        else if (HasComp<ModSuitPartDeployableComponent>(ent.Owner))
            _module.UpdatePowerDraw(ent.Owner);
    }

    private void OnToggleComponentSealed(Entity<ToggleableComponentModSuitPartDeployableAllSealedComponent> ent, ref ModSuitContainerPartSealedEvent args)
    {
        var partDeployable = Comp<ModSuitPartDeployableComponent>(ent.Owner);
        var parts = GetDeployedParts((ent.Owner, partDeployable)).ToArray();
        if (parts.Length > partDeployable.DeployableContainers.Count)
            return;

        var ev = new ModSuitAllSealedComponentsUpdatedEvent();

        if (ent.Comp.AllPartComponents != null)
        {
            foreach (var part in parts)
            {
                EntityManager.AddComponents(part, ent.Comp.AllPartComponents);
                RaiseLocalEvent(part, ev);
            }

            EntityManager.AddComponents(ent.Owner, ent.Comp.AllPartComponents);

            if (ent.Comp.DeployerComponents == null)
                RaiseLocalEvent(ent.Owner, ev);
        }

        if (ent.Comp.DeployerComponents != null)
        {
            EntityManager.AddComponents(ent.Owner, ent.Comp.DeployerComponents);
            RaiseLocalEvent(ent.Owner, ev);
        }

        ent.Comp.AllSealed = true;
        Dirty(ent);
    }

    private void OnToggleComponentUnsealed(Entity<ToggleableComponentModSuitPartDeployableAllSealedComponent> ent, ref ModSuitContainerPartUnsealedEvent args)
    {
        if (!ent.Comp.AllSealed)
            return;

        var ev = new ModSuitAllSealedComponentsUpdatedEvent();

        if (ent.Comp.AllPartComponents != null)
        {
            foreach (var part in GetAllParts(ent.Owner))
            {
                RaiseLocalEvent(part, ev);
                EntityManager.RemoveComponents(part, ent.Comp.AllPartComponents);
            }

            if (ent.Comp.DeployerComponents == null)
                RaiseLocalEvent(ent.Owner, ev);

            EntityManager.RemoveComponents(ent.Owner, ent.Comp.AllPartComponents);
        }

        if (ent.Comp.DeployerComponents != null)
        {
            RaiseLocalEvent(ent.Owner, ev);
            EntityManager.RemoveComponents(ent.Owner, ent.Comp.DeployerComponents);
        }

        ent.Comp.AllSealed = false;
        Dirty(ent);
    }

    private void OnSealableUiButton(Entity<ModSuitUserInterfaceComponent> ent, ref ModSuitSealButtonMessage args)
    {
        if (args.Parts.Count > MaxSealButtonParts)
            return;

        var i = 0;
        foreach (var (netPart, shouldSeal) in args.Parts)
        {
            if (!TryGetEntity(netPart, out var part) || !IsPartOfSuit(part.Value, ent.Owner))
                continue;

            SetSeal(part.Value, shouldSeal, i++);
        }
    }

    /// <summary>
    /// Whether the entity is the suit itself or one of its deployed parts.
    /// </summary>
    [PublicAPI]
    public bool IsPartOfSuit(EntityUid part, EntityUid suit)
    {
        return part == suit || CompOrNull<ModSuitDeployedPartComponent>(part)?.Suit == suit;
    }

    [PublicAPI]
    public bool IsSealed(Entity<ModSuitSealableComponent?> ent)
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
            return false;

        return ent.Comp.Sealed;
    }

    [PublicAPI]
    public bool SetSeal(Entity<ModSuitSealableComponent?> ent, bool shouldSeal, int sealedPartCount)
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
            return false;

        // prevent sending events and excessive dirtying
        if (ent.Comp.Sealed == shouldSeal)
            return true;

        if (!IsDelayed((ent.Owner, ent.Comp), sealedPartCount, out var delay))
            return SetSeal(ent, shouldSeal);

        if (!RaiseAttemptSealEvent(ent!, shouldSeal, sealedPartCount))
            return false;

        EnsureComp<ModSuitSealablePendingComponent>(ent.Owner, out var pendingComp);
        pendingComp.NextUpdate = _timing.CurTime + delay.Value;
        pendingComp.ShouldSeal = shouldSeal;
        Dirty(ent.Owner, pendingComp);
        return true;
    }

    [PublicAPI]
    public bool SetSeal(Entity<ModSuitSealableComponent?> ent, bool shouldSeal)
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
            return false;

        // prevent sending events and excessive dirtying
        if (ent.Comp.Sealed == shouldSeal)
            return true;

        if (!RaiseAttemptSealEvent(ent!, shouldSeal, 0))
            return false;

        ent.Comp.Sealed = shouldSeal;
        RemCompDeferred<ModSuitSealablePendingComponent>(ent.Owner); // stop any pending updates overwriting us
        Dirty(ent);

        var container = CompOrNull<ModSuitDeployedPartComponent>(ent.Owner)?.Suit ?? ent.Owner;

        if (shouldSeal)
        {
            EntityManager.AddComponents(ent.Owner, ent.Comp.SealedComponents, true);

            var partEv = new ModSuitSealedEvent(ent.Comp.Wearer);
            RaiseLocalEvent(ent.Owner, partEv);

            var suitEv = new ModSuitContainerPartSealedEvent(ent.Owner);
            RaiseLocalEvent(container, suitEv);
        }
        else
        {
            var partEv = new ModSuitUnsealedEvent(ent.Comp.Wearer);
            RaiseLocalEvent(ent.Owner, partEv);

            var suitEv = new ModSuitContainerPartUnsealedEvent(ent.Owner);
            RaiseLocalEvent(container, suitEv);

            EntityManager.RemoveComponents(ent.Owner, ent.Comp.SealedComponents);
        }

        if (ent.Comp.Wearer is { } wearer)
        {
            var wearerEv = new ModSuitWearerSealChangedEvent(ent.Owner, shouldSeal);
            RaiseLocalEvent(wearer, ref wearerEv);
        }

        PlaySound((ent.Owner, ent.Comp), shouldSeal);

        Appearance.SetData(ent.Owner, ModSuitSealedVisuals.Sealed, shouldSeal);
        UpdateUI(container);

        return true;
    }

    private bool RaiseAttemptSealEvent(Entity<ModSuitSealableComponent> ent, bool shouldSeal, int sealedPartCount)
    {
        var ev = new ModSuitSealAttemptEvent(shouldSeal, sealedPartCount);
        RaiseLocalEvent(ent.Owner, ev);

        return !ev.Cancelled;
    }

    private void PlaySound(Entity<ModSuitSealableComponent> ent, bool shouldSeal)
    {
        if (!_net.IsServer)
            return;

        if (shouldSeal)
            _audio.PlayPvs(ent.Comp.SealSound, ent.Owner);
        else
            _audio.PlayPvs(ent.Comp.UnsealSound, ent.Owner);
    }

    private static bool IsDelayed(Entity<ModSuitSealableComponent> ent, int sealedPartCount, [NotNullWhen(true)] out TimeSpan? delay)
    {
        delay = null;

        if (ent.Comp.DelayPerPart is not { } delayPerPart)
            return false;

        if (sealedPartCount > 0)
        {
            delay = delayPerPart * sealedPartCount;
            return true;
        }

        return false;
    }
}
