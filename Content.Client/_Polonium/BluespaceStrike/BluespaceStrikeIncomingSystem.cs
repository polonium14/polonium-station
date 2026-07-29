// SPDX-FileCopyrightText: 2026 Nikita (Nick) <174215049+nikitosych@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared._Polonium.BluespaceStrike;
using Robust.Client.ComponentTrees;
using Robust.Client.GameObjects;
using Robust.Client.Timing;
using Robust.Shared.Timing;

namespace Content.Client._Polonium.BluespaceStrike;

/// <summary>
/// Animates the incoming bluespace projectile falling from above onto the strike epicenter.
/// Uses last-applied server tick time so prediction clock lead doesnt land the bolt early.
/// </summary>
public sealed partial class BluespaceStrikeIncomingSystem : EntitySystem
{
    [Dependency] private IClientGameTiming _timing = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private static readonly Vector2 StartScale = new(5f, 5f);
    private static readonly Vector2 EndScale = new(2.5f, 2.5f);

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(SpriteTreeSystem));
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var now = GetServerSyncedTime();

        var query = EntityQueryEnumerator<BluespaceStrikeIncomingComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var comp, out var sprite))
        {
            if (comp.ImpactAt == default)
                continue;

            var duration = (float)comp.FallDuration.TotalSeconds;
            if (duration <= 0f)
                duration = BluespaceStrikeComponent.FallDurationSeconds;

            var remaining = (float)(comp.ImpactAt - now).TotalSeconds;
            var progress = Math.Clamp(1f - remaining / duration, 0f, 1f);

            var eased = progress * progress;
            var offset = new Vector2(0f, MathHelper.Lerp(comp.StartOffsetY, 0f, eased));
            var scale = Vector2.Lerp(StartScale, EndScale, eased);

            _sprite.SetVisible((uid, sprite), true);
            _sprite.SetOffset((uid, sprite), offset);
            _sprite.SetScale((uid, sprite), scale);
        }
    }

    private TimeSpan GetServerSyncedTime()
    {
        var ticksAhead = _timing.CurTick.Value - _timing.LastRealTick.Value;
        if (ticksAhead <= 0)
            return _timing.CurTime;

        return _timing.CurTime - _timing.TickPeriod * ticksAhead;
    }
}
