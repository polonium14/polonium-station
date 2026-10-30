using Content.Shared.Buckle.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Utility;

namespace Content.Shared._Polonium.Medical.OperatingTable;

public sealed partial class OperatingTableLightSystem : EntitySystem
{
    /// <summary>
    /// Used when the patient has no critical threshold to measure health against.
    /// </summary>
    private const float DefaultMaxHealth = 100f;

    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MobThresholdSystem _mobThreshold = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPointLightSystem _pointLight = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OperatingTableLightComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<OperatingTableLightComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<OperatingTableLightComponent, StrappedEvent>(OnStrapped);
        SubscribeLocalEvent<OperatingTableLightComponent, UnstrappedEvent>(OnUnstrapped);

        // Only the patient on the table carries OperatingTableMonitoredComponent, so these do not fire for every
        // entity in the world. They also leave the shared <DamageableComponent, DamageChangedEvent> pair free.
        SubscribeLocalEvent<OperatingTableMonitoredComponent, DamageChangedEvent>(OnPatientDamageChanged,
            after: [typeof(MobThresholdSystem)]);
        SubscribeLocalEvent<OperatingTableMonitoredComponent, MobStateChangedEvent>(OnPatientMobStateChanged);
    }

    private void OnStartup(Entity<OperatingTableLightComponent> ent, ref ComponentStartup args)
    {
        // A table saved with its light on must look and shine the same after loading.
        _appearance.SetData(ent, OperatingTableVisuals.LightOn, ent.Comp.LightOn);
        _pointLight.SetEnabled(ent, ent.Comp.LightOn);
    }

    private void OnGetVerbs(Entity<OperatingTableLightComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(ent.Comp.LightOn ? "operating-table-light-turn-off" : "operating-table-light-turn-on"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/light.svg.192dpi.png")),
            Priority = -1,
            Act = () => ToggleLight(ent, user),
        });
    }

    private void ToggleLight(Entity<OperatingTableLightComponent> ent, EntityUid user)
    {
        ent.Comp.LightOn = !ent.Comp.LightOn;
        Dirty(ent);

        _appearance.SetData(ent, OperatingTableVisuals.LightOn, ent.Comp.LightOn);
        _pointLight.SetEnabled(ent, ent.Comp.LightOn);
        _audio.PlayPredicted(ent.Comp.ToggleSound, ent, user);
    }

    private void OnStrapped(Entity<OperatingTableLightComponent> ent, ref StrappedEvent args)
    {
        var patient = args.Buckle.Owner;

        var monitored = EnsureComp<OperatingTableMonitoredComponent>(patient);
        monitored.Table = ent;
        Dirty(patient, monitored);

        UpdateMonitor(ent, patient);
    }

    private void OnUnstrapped(Entity<OperatingTableLightComponent> ent, ref UnstrappedEvent args)
    {
        RemComp<OperatingTableMonitoredComponent>(args.Buckle.Owner);

        if (args.Strap.Comp.BuckledEntities.Count != 0)
            return;

        _appearance.SetData(ent, OperatingTableVisuals.VitalsState, VitalsState.None);
        SetSound(ent, VitalsState.None);
    }

    private void OnPatientDamageChanged(Entity<OperatingTableMonitoredComponent> ent, ref DamageChangedEvent args)
    {
        UpdateMonitor(ent);
    }

    private void OnPatientMobStateChanged(Entity<OperatingTableMonitoredComponent> ent, ref MobStateChangedEvent args)
    {
        UpdateMonitor(ent);
    }

    private void UpdateMonitor(Entity<OperatingTableMonitoredComponent> patient)
    {
        if (TryComp<OperatingTableLightComponent>(patient.Comp.Table, out var table))
            UpdateMonitor((patient.Comp.Table, table), patient);
    }

    private void UpdateMonitor(Entity<OperatingTableLightComponent> table, EntityUid patient)
    {
        var state = GetVitals(table.Comp, patient);
        _appearance.SetData(table, OperatingTableVisuals.VitalsState, state);
        SetSound(table, state);
    }

    private VitalsState GetVitals(OperatingTableLightComponent table, EntityUid patient)
    {
        if (!TryComp<MobStateComponent>(patient, out var mobState))
            return VitalsState.None;

        if (_mobState.IsDead(patient, mobState))
            return VitalsState.Dead;

        if (_mobState.IsCritical(patient, mobState))
            return VitalsState.Critical;

        if (!TryComp<DamageableComponent>(patient, out var damageable))
            return VitalsState.Healthy;

        var maxHealth = DefaultMaxHealth;
        if (_mobThreshold.TryGetThresholdForState(patient, MobState.Critical, out var critThreshold)
            && critThreshold.Value > FixedPoint2.Zero)
        {
            maxHealth = critThreshold.Value.Float();
        }

        var health = 1f - _damageable.GetTotalDamage((patient, damageable)).Float() / maxHealth;
        if (health > table.HealthyThreshold)
            return VitalsState.Healthy;

        return health > table.InjuredThreshold ? VitalsState.Injured : VitalsState.Critical;
    }

    private void SetSound(Entity<OperatingTableLightComponent> table, VitalsState state)
    {
        // Looping sounds are entities that only the server creates. A client that predicts this
        // would start a second copy that nothing ever stops.
        if (_net.IsClient || table.Comp.SoundState == state)
            return;

        table.Comp.SoundState = state;
        table.Comp.MonitorStream = _audio.Stop(table.Comp.MonitorStream);

        var sound = state switch
        {
            VitalsState.Healthy => table.Comp.HeartbeatHealthySound,
            VitalsState.Injured => table.Comp.HeartbeatInjuredSound,
            VitalsState.Critical => table.Comp.HeartbeatCriticalSound,
            VitalsState.Dead => table.Comp.FlatlineSound,
            _ => null,
        };

        if (sound != null)
            table.Comp.MonitorStream = _audio.PlayPvs(sound, table)?.Entity;
    }
}
