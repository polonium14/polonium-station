// SPDX-FileCopyrightText: 2026 Nikita (Nick) <174215049+nikitosych@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Eui;
using Content.Shared.Explosion.Components;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.BluespaceStrike;

public static class BluespaceStrikeEuiMsg
{
    [Serializable, NetSerializable]
    public sealed class PreviewRequest : EuiMessageBase
    {
        public readonly MapCoordinates Epicenter;
        public readonly float Radius;

        public PreviewRequest(MapCoordinates epicenter, float radius)
        {
            Epicenter = epicenter;
            Radius = radius;
        }
    }

    [Serializable, NetSerializable]
    public sealed class PreviewData : EuiMessageBase
    {
        public readonly float Slope;
        public readonly float TotalIntensity;
        public readonly ExplosionVisualsState Explosion;

        public PreviewData(ExplosionVisualsState explosion, float slope, float totalIntensity)
        {
            Slope = slope;
            TotalIntensity = totalIntensity;
            Explosion = explosion;
        }
    }

    [Serializable, NetSerializable]
    public sealed class Confirm : EuiMessageBase
    {
        public readonly MapCoordinates Epicenter;
        public readonly float Radius;
        public readonly float DelaySeconds;

        public readonly bool ShowMarkersAndSound;

        public Confirm(MapCoordinates epicenter, float radius, float delaySeconds, bool showMarkersAndSound)
        {
            Epicenter = epicenter;
            Radius = radius;
            DelaySeconds = delaySeconds;
            ShowMarkersAndSound = showMarkersAndSound;
        }
    }

    [Serializable, NetSerializable]
    public sealed class PlayArtillerySound : EuiMessageBase;
}

[Serializable, NetSerializable]
public sealed class BluespaceStrikeEuiState : EuiStateBase
{
    public bool IsEpsilon;
    public string CurrentAlertLevel = string.Empty;
}

