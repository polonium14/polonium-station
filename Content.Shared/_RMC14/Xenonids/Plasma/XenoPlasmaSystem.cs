using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Shared.Alert;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Content.Shared.Rounding;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Shared._RMC14.Xenonids.Plasma;

public sealed class XenoPlasmaSystem : EntitySystem
{
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private EntityQuery<XenoPlasmaComponent> _xenoPlasmaQuery;

    public override void Initialize()
    {
        _xenoPlasmaQuery = GetEntityQuery<XenoPlasmaComponent>();

        SubscribeLocalEvent<XenoPlasmaComponent, MapInitEvent>(OnXenoPlasmaMapInit);
        SubscribeLocalEvent<XenoPlasmaComponent, ComponentRemove>(OnXenoPlasmaRemove);
        SubscribeLocalEvent<XenoPlasmaComponent, NewXenoEvolvedEvent>(OnNewXenoEvolved);
        SubscribeLocalEvent<XenoPlasmaComponent, XenoDevolvedEvent>(OnXenoDevolved);
        SubscribeLocalEvent<XenoPlasmaComponent, PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<XenoPlasmaComponent, AfterNewXenoEvolvedEvent>(OnAfterEvolved);
    }

    private void OnXenoPlasmaMapInit(Entity<XenoPlasmaComponent> ent, ref MapInitEvent args)
    {
        UpdateAlert(ent);
    }

    private void OnXenoPlasmaRemove(Entity<XenoPlasmaComponent> ent, ref ComponentRemove args)
    {
        _alerts.ClearAlert(ent.Owner, ent.Comp.Alert);
    }

    private void OnNewXenoEvolved(Entity<XenoPlasmaComponent> newXeno, ref NewXenoEvolvedEvent args)
    {
        EvolutionTransferPlasma(args.OldXeno, newXeno);
    }

    private void OnXenoDevolved(Entity<XenoPlasmaComponent> newXeno, ref XenoDevolvedEvent args)
    {
        EvolutionTransferPlasma(args.OldXeno, newXeno);
    }

    private void OnPlayerAttached(Entity<XenoPlasmaComponent> ent, ref PlayerAttachedEvent args)
    {
        RefreshAlert(ent);
    }

    private void OnAfterEvolved(Entity<XenoPlasmaComponent> ent, ref AfterNewXenoEvolvedEvent args)
    {
        RefreshAlert(ent);
    }

    private void EvolutionTransferPlasma(EntityUid oldXeno, Entity<XenoPlasmaComponent> newXeno)
    {
        if (!TryComp(oldXeno, out XenoPlasmaComponent? oldPlasma))
            return;

        FixedPoint2 newPlasma = newXeno.Comp.MaxPlasma;
        if (oldPlasma.MaxPlasma > 0)
            newPlasma *= oldPlasma.Plasma / oldPlasma.MaxPlasma;

        SetPlasma(newXeno, newPlasma);
    }

    // clear+show so ShowAlert always Dirties even if severity didnt change
    private void RefreshAlert(Entity<XenoPlasmaComponent> xeno)
    {
        _alerts.ClearAlert(xeno.Owner, xeno.Comp.Alert);
        UpdateAlert(xeno);
    }

    private void UpdateAlert(Entity<XenoPlasmaComponent> xeno)
    {
        if (xeno.Comp.MaxPlasma == 0)
        {
            _alerts.ClearAlert(xeno.Owner, xeno.Comp.Alert);
            return;
        }

        var level = MathF.Max(0f, xeno.Comp.Plasma.Float());
        var max = _alerts.GetMaxSeverity(xeno.Comp.Alert);
        var severity = max - ContentHelpers.RoundToLevels(level, xeno.Comp.MaxPlasma, max + 1);
        _alerts.ShowAlert(xeno.Owner, xeno.Comp.Alert, (short)severity);
    }

    public bool HasPlasma(Entity<XenoPlasmaComponent> xeno, FixedPoint2 plasma)
    {
        return xeno.Comp.Plasma >= plasma;
    }

    public bool HasPlasmaPopup(Entity<XenoPlasmaComponent?> xeno, FixedPoint2 plasma, bool predicted = true)
    {
        if (!Resolve(xeno, ref xeno.Comp, false) || !HasPlasma((xeno, xeno.Comp), plasma))
        {
            var msg = Loc.GetString("cm-xeno-not-enough-plasma");
            if (predicted)
                _popup.PopupClient(msg, xeno, xeno, PopupType.MediumCaution);
            else
                _popup.PopupEntity(msg, xeno, xeno, PopupType.MediumCaution);
            return false;
        }

        return true;
    }

    public FixedPoint2 RegenPlasma(Entity<XenoPlasmaComponent?> xeno, FixedPoint2 amount)
    {
        if (!_xenoPlasmaQuery.Resolve(xeno, ref xeno.Comp, false))
            return FixedPoint2.Zero;

        var old = xeno.Comp.Plasma;
        xeno.Comp.Plasma = FixedPoint2.Min(xeno.Comp.Plasma + amount, xeno.Comp.MaxPlasma);

        if (old == xeno.Comp.Plasma)
            return FixedPoint2.Zero;

        Dirty(xeno);
        UpdateAlert((xeno, xeno.Comp));
        return xeno.Comp.Plasma - old;
    }

    public void RemovePlasma(Entity<XenoPlasmaComponent> xeno, FixedPoint2 plasma)
    {
        xeno.Comp.Plasma = FixedPoint2.Max(FixedPoint2.Zero, xeno.Comp.Plasma - plasma);
        Dirty(xeno);
        UpdateAlert(xeno);
    }

    public void SetPlasma(Entity<XenoPlasmaComponent> xeno, FixedPoint2 plasma)
    {
        xeno.Comp.Plasma = plasma;
        Dirty(xeno);
        UpdateAlert(xeno);
    }

    public bool TryRemovePlasma(Entity<XenoPlasmaComponent?> xeno, FixedPoint2 plasma)
    {
        if (!Resolve(xeno, ref xeno.Comp, false))
            return false;

        if (!HasPlasma((xeno, xeno.Comp), plasma))
            return false;

        RemovePlasma((xeno, xeno.Comp), plasma);
        return true;
    }

    public bool TryRemovePlasmaPopup(Entity<XenoPlasmaComponent?> xeno, FixedPoint2 plasma, bool predicted = true)
    {
        if (!Resolve(xeno, ref xeno.Comp, false))
            return false;

        if (TryRemovePlasma((xeno, xeno.Comp), plasma))
            return true;

        var msg = Loc.GetString("cm-xeno-not-enough-plasma");
        if (predicted)
            _popup.PopupClient(msg, xeno, xeno, PopupType.MediumCaution);
        else if (_net.IsServer)
            _popup.PopupEntity(msg, xeno, xeno, PopupType.MediumCaution);

        return false;
    }
}
