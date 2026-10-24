using Content.Shared._Polonium.Modules.ModSuit.Components;
using Content.Shared._Polonium.Modules.ModSuit.Events;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Strip;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Shared._Polonium.Modules.ModSuit;

public partial class SharedModSuitSystem
{
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStrippableSystem _strippable = default!;

    private static readonly SpriteSpecifier SealVerbIcon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/outfit.svg.192dpi.png"));
    private static readonly VerbCategory SealVerbCategory = new("verb-categories-modsuit-seal", "/Textures/Interface/VerbIcons/outfit.svg.192dpi.png");

    private void InitializeSealVerb()
    {
        SubscribeLocalEvent<ModSuitSealableComponent, GetVerbsEvent<EquipmentVerb>>(OnSealableGetVerbs);
        SubscribeLocalEvent<ModSuitSealableComponent, InventoryRelayedEvent<GetVerbsEvent<EquipmentVerb>>>(OnSealableGetRelayedVerbs);
        SubscribeLocalEvent<ModSuitSealableComponent, ModSuitSealDoAfterEvent>(OnSealDoAfter);
    }

    private void OnSealableGetVerbs(Entity<ModSuitSealableComponent> ent, ref GetVerbsEvent<EquipmentVerb> args)
    {
        AddSealVerb(ent, args);
    }

    private void OnSealableGetRelayedVerbs(Entity<ModSuitSealableComponent> ent, ref InventoryRelayedEvent<GetVerbsEvent<EquipmentVerb>> args)
    {
        AddSealVerb(ent, args.Args);
    }

    private void AddSealVerb(Entity<ModSuitSealableComponent> ent, GetVerbsEvent<EquipmentVerb> args)
    {
        // The wearer seals the suit from its panel, this is for doing it to someone else.
        if (ent.Comp.Wearer is not { } wearer || args.User == wearer || !args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        var user = args.User;
        var shouldSeal = !ent.Comp.Sealed;
        args.Verbs.Add(new EquipmentVerb
        {
            Text = Loc.GetString(shouldSeal ? "modsuit-seal-other-verb" : "modsuit-unseal-other-verb", ("part", ent.Owner)),
            Icon = SealVerbIcon,
            Category = SealVerbCategory,
            Act = () => StartSealOther(ent, user, wearer, shouldSeal),
        });
    }

    private void StartSealOther(Entity<ModSuitSealableComponent> ent, EntityUid user, EntityUid wearer, bool shouldSeal)
    {
        var (time, stealth) = _strippable.GetStripTimeModifiers(user, wearer, ent.Owner, ent.Comp.SealOtherDelay);

        var doAfterArgs = new DoAfterArgs(EntityManager, user, time, new ModSuitSealDoAfterEvent(shouldSeal), ent.Owner, wearer, ent.Owner)
        {
            Hidden = stealth,
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            DuplicateCondition = DuplicateConditions.SameEvent,
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs))
            return;

        if (!stealth)
        {
            _popup.PopupEntity(
                Loc.GetString(shouldSeal ? "modsuit-seal-alert-owner" : "modsuit-unseal-alert-owner", ("user", Identity.Entity(user, EntityManager)), ("part", ent.Owner)),
                wearer,
                wearer,
                PopupType.Large);
        }

        _adminLog.Add(LogType.Stripping,
            LogImpact.Low,
            $"{ToPrettyString(user):actor} is trying to {(shouldSeal ? "seal" : "unseal")} {ToPrettyString(ent.Owner):item} on {ToPrettyString(wearer):target}");
    }

    private void OnSealDoAfter(Entity<ModSuitSealableComponent> ent, ref ModSuitSealDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || ent.Comp.Wearer is not { } wearer)
            return;

        args.Handled = true;

        if (ent.Comp.Sealed == args.ShouldSeal)
            return;

        if (!SetSeal((ent.Owner, ent.Comp), args.ShouldSeal))
        {
            _popup.PopupEntity(Loc.GetString("modsuit-seal-other-no-power", ("part", ent.Owner)), wearer, args.User);
            return;
        }

        _adminLog.Add(LogType.Stripping,
            LogImpact.Medium,
            $"{ToPrettyString(args.User):actor} {(args.ShouldSeal ? "sealed" : "unsealed")} {ToPrettyString(ent.Owner):item} on {ToPrettyString(wearer):target}");
    }
}
