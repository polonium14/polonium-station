// SPDX-FileCopyrightText: 2024 Jake Huxell <JakeHuxell@pm.me>
// SPDX-FileCopyrightText: 2024 Plykiya <58439124+Plykiya@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Fildrance <fildrance@gmail.com>
// SPDX-FileCopyrightText: 2025 Samuka <47865393+Samuka-C@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Pieter-Jan Briers <pieterjan.briers+git@gmail.com>
// SPDX-FileCopyrightText: 2026 Velken <8467292+Velken@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Whatstone <166147148+whatston3@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 lunarcomets <140772713+lunarcomets@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 taydeo <tay@funkystation.org>
// SPDX-FileCopyrightText: 2026 taydeo <td12233a@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Funkystation.FirelockBolt.Components;
using Content.Shared._Funkystation.FirelockBolt.EntitySystems;
using Content.Shared.Access.Components;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Electrocution;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Remotes.Components;
using Content.Shared.Tag;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared.Remotes.EntitySystems;

public abstract partial class SharedDoorRemoteSystem : EntitySystem
{
    [Dependency] private SharedAirlockSystem _airlock = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDoorSystem _doorSystem = default!;
    [Dependency] private SharedElectrocutionSystem _electrify = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private SharedPowerReceiverSystem _powerReceiver = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private TagSystem _tagSystem = default!;
    [Dependency] private SharedFirelockBoltControlSystem _firelockBolts = default!;
    [Dependency] protected IGameTiming Timing = default!;


    public override void Initialize()
    {
        SubscribeLocalEvent<DoorRemoteComponent, DoorRemoteModeChangeMessage>(OnDoorRemoteModeChange);
        SubscribeLocalEvent<DoorRemoteComponent, BeforeRangedInteractEvent>(OnBeforeInteract);
    }

    private void OnDoorRemoteModeChange(Entity<DoorRemoteComponent> ent, ref DoorRemoteModeChangeMessage args)
    {
        ent.Comp.Mode = args.Mode;
        Dirty(ent);
    }

    private void OnBeforeInteract(Entity<DoorRemoteComponent> entity, ref BeforeRangedInteractEvent args)
    {
        if (!Timing.IsFirstTimePredicted)
            return;

        var isAirlock = TryComp<AirlockComponent>(args.Target, out var airlockComp);

        if (args.Handled
            || args.Target == null
            || !TryComp<DoorComponent>(args.Target, out var doorComp) // If it isn't a door we don't use it
                                                                      // Only able to control doors if they are within your vision and within your max range.
                                                                      // Not affected by mobs or machines anymore.
            || (entity.Comp.RequireInRangeUnoccluded && !_examine.InRangeUnOccluded(args.User,
                args.Target.Value,
                SharedInteractionSystem.MaxRaycastRange,
                null)))

        {
            return;
        }

        args.Handled = true;

        if (!_powerReceiver.IsPowered(args.Target.Value))
        {
            _popup.PopupEntity(Loc.GetString("door-remote-no-power"), args.User, args.User);
            return;
        }

        var accessTarget = args.Used;
        // This covers the accesses the REMOTE has, and is not effected by the user's ID card.
        if (entity.Comp.IncludeUserAccess) // Allows some door remotes to inherit the user's access.
        {
            accessTarget = args.User;
            // This covers the accesses the USER has, which always includes the remote's access since holding a remote acts like holding an ID card.
        }

        // Only let remote work on doors that have AccessReader; otherwise, it works on anything with a Door component (curtains, fence gates, etc)
        if (TryComp<AccessReaderComponent>(args.Target, out var accessComponent) && _tagSystem.HasTag(args.Target.Value, entity.Comp.TargetTag))
        {
            // Has an access reader component. Check access.
            if (!_doorSystem.HasAccess(args.Target.Value, accessTarget, doorComp, accessComponent))
            {
                if (isAirlock)
                    _doorSystem.Deny(args.Target.Value, doorComp, user: args.User, predicted: true);

                _popup.PopupEntity(Loc.GetString("door-remote-denied"), args.User, args.User);
                return;
            }
        }
        // Unless allowed to bypass by the flag on the component.
        else if (entity.Comp.RequireTagWhitelist)
            return;

        switch (entity.Comp.Mode)
        {
            case OperatingMode.OpenClose:
                if (_doorSystem.TryToggleDoor(args.Target.Value, doorComp, user: args.User, predicted: true))
                    _adminLogger.Add(LogType.Action,
                        LogImpact.Medium,
                        $"{ToPrettyString(args.User):player} used {ToPrettyString(args.Used)} on {ToPrettyString(args.Target.Value)}: {doorComp.State}");
                break;
            case OperatingMode.ToggleBolts:
                if (TryComp<DoorBoltComponent>(args.Target, out var boltsComp))
                {
                    if (!boltsComp.BoltWireCut)
                    {
                        var willBolt = !boltsComp.BoltsDown;

                        if (TryComp<FirelockBoltControlComponent>(args.Target, out var firelockBolts))
                        {
                            _firelockBolts.SetOverride((args.Target.Value, firelockBolts), !willBolt, playSound: false);
                            
                            if (willBolt)
                                _doorSystem.SetBoltsDown((args.Target.Value, boltsComp), true, user: args.User, predicted: true);
                        }
                        else
                        {
                            _doorSystem.SetBoltsDown((args.Target.Value, boltsComp), willBolt, user: args.User, predicted: true);
                        }

                        _adminLogger.Add(LogType.Action,
                            LogImpact.Medium,
                            $"{ToPrettyString(args.User):player} used {ToPrettyString(args.Used)} on {ToPrettyString(args.Target.Value)} to {(willBolt ? "" : "un")}bolt it");
                    }
                }

                break;
            case OperatingMode.ToggleEmergencyAccess:
                if (airlockComp != null)
                {
                    _airlock.SetEmergencyAccess((args.Target.Value, airlockComp), !airlockComp.EmergencyAccess, user: args.User, predicted: true);
                    _adminLogger.Add(LogType.Action,
                        LogImpact.Medium,
                        $"{ToPrettyString(args.User):player} used {ToPrettyString(args.Used)} on {ToPrettyString(args.Target.Value)} to set emergency access {(airlockComp.EmergencyAccess ? "on" : "off")}");
                }

                break;
            case OperatingMode.ToggleOvercharge:
                if (TryComp<ElectrifiedComponent>(args.Target, out var eletrifiedComp))
                {
                    _electrify.SetElectrified((args.Target.Value, eletrifiedComp), !eletrifiedComp.Enabled);
                    var soundToPlay = eletrifiedComp.Enabled
                        ? eletrifiedComp.AirlockElectrifyEnabled
                        : eletrifiedComp.AirlockElectrifyDisabled;
                    _audio.PlayLocal(soundToPlay, args.Target.Value, args.User);
                    _adminLogger.Add(LogType.Action,
                        LogImpact.Medium,
                        $"{ToPrettyString(args.User):player} used {ToPrettyString(args.Used)} on {ToPrettyString(args.Target.Value)} to {(eletrifiedComp.Enabled ? "" : "un")}electrify it");
                }

                break;
            default:
                throw new InvalidOperationException(
                    $"{nameof(DoorRemoteComponent)} had invalid mode {entity.Comp.Mode}");
        }
    }
}

[Serializable, NetSerializable]
public sealed class DoorRemoteModeChangeMessage : BoundUserInterfaceMessage
{
    public OperatingMode Mode;
}

[Serializable, NetSerializable]
public enum DoorRemoteUiKey : byte
{
    Key
}
