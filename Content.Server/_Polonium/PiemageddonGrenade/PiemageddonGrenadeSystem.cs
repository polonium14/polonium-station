// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared._Polonium.PiemageddonGrenade;
using Content.Shared.Throwing;
using Content.Shared.Trigger;
using Content.Shared.Trigger.Components;
using Content.Shared.Trigger.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Map;

namespace Content.Server._Polonium.PiemageddonGrenade;

public sealed partial class PiemageddonGrenadeSystem : EntitySystem
{
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private TriggerSystem _trigger = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PiemageddonGrenadeComponent, TriggerEvent>(OnTrigger);
    }

    private void OnTrigger(Entity<PiemageddonGrenadeComponent> entity, ref TriggerEvent args)
    {
        if (args.Key != entity.Comp.TriggerKey)
            return;

        entity.Comp.IsTriggered = true;
        args.Handled = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Collect triggered grenades first. Scattering happens after the enumeration so that
        // spawning a child PiemageddonGrenade doesn't mutate the component dictionary mid-query.
        var triggered = new List<(EntityUid Uid, PiemageddonGrenadeComponent Comp)>();
        var query = EntityQueryEnumerator<PiemageddonGrenadeComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.IsTriggered)
                triggered.Add((uid, comp));
        }

        foreach (var (uid, comp) in triggered)
        {
            Scatter(uid, comp);
        }
    }

    private void Scatter(EntityUid uid, PiemageddonGrenadeComponent comp)
    {
        if (comp.FillPrototype is not { } fill)
        {
            // Nothing to scatter; the grenade just removes itself.
            Del(uid);
            return;
        }

        var coords = _transform.GetMapCoordinates(uid);
        var segmentAngle = 360f / comp.Count;

        for (var i = 0; i < comp.Count; i++)
        {
            var child = Spawn(fill, coords);

            var angle = Angle.FromDegrees(segmentAngle * i);
            var direction = angle.ToVec().Normalized() * comp.Distance;
            _throwing.TryThrow(child, direction, comp.Velocity);

            if (comp.TriggerContents && TryComp<TimerTriggerComponent>(child, out var timer))
            {
                _trigger.SetDelay((child, timer), TimeSpan.FromSeconds(comp.DelayBeforeTriggerContents));
                _trigger.ActivateTimerTrigger((child, timer));
            }
        }

        // DeleteOnTrigger can't be used because scattering is deferred to the next frame update.
        Del(uid);
    }
}
