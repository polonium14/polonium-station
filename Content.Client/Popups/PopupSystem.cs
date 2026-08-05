// SPDX-FileCopyrightText: 2021 Paul <ritter.paul1@googlemail.com>
// SPDX-FileCopyrightText: 2021 Vera Aguilera Puerto <6766154+Zumorica@users.noreply.github.com>
// SPDX-FileCopyrightText: 2021 Vera Aguilera Puerto <gradientvera@outlook.com>
// SPDX-FileCopyrightText: 2022 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022 Jack Fox <35575261+DubiousDoggo@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022 Kara <lunarautomaton6@gmail.com>
// SPDX-FileCopyrightText: 2022 Visne <39844191+Visne@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022 metalgearsloth <comedian_vs_clown@hotmail.com>
// SPDX-FileCopyrightText: 2022 mirrorcult <lunarautomaton6@gmail.com>
// SPDX-FileCopyrightText: 2023 Leon Friedrich <60421075+ElectroJr@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 Vordenburg <114301317+Vordenburg@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 deltanedas <39013340+deltanedas@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 deltanedas <@deltanedas:kde.org>
// SPDX-FileCopyrightText: 2024 DrSmugleaf <10968691+DrSmugleaf@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 LordCarve <27449516+LordCarve@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Nemanja <98561806+EmoGarbage404@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 ShadowCommander <10494922+ShadowCommander@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Cooper Wallace <6856074+CooperWallace@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Milon <milonpl.git@proton.me>
// SPDX-FileCopyrightText: 2025 Tayrtahn <tayrtahn@gmail.com>
// SPDX-FileCopyrightText: 2026 Pieter-Jan Briers <pieterjan.briers+git@gmail.com>
// SPDX-FileCopyrightText: 2026 Whatstone <166147148+whatston3@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 slarticodefast <161409025+slarticodefast@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 taydeo <tay@funkystation.org>
// SPDX-FileCopyrightText: 2026 taydeo <td12233a@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Client.UserInterface.Systems.Chat;
using Content.Shared._Goobstation.CCVar;
using Content.Shared.Chat;
using Content.Shared.Examine;
using Content.Shared.GameTicking;
using Content.Shared.Popups;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Collections;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Replays;
using Robust.Shared.Utility;

namespace Content.Client.Popups;

public sealed partial class PopupSystem : SharedPopupSystem
{
    [Dependency] private IConfigurationManager _configManager = default!;
    [Dependency] private IInputManager _inputManager = default!;
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IUserInterfaceManager _uiManager = default!;
    [Dependency] private IReplayRecordingManager _replayRecording = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public IReadOnlyCollection<WorldPopupLabel> WorldLabels => _aliveWorldLabels.Values;
    public IReadOnlyCollection<CursorPopupLabel> CursorLabels => _aliveCursorLabels.Values;

    private readonly Dictionary<WorldPopupData, WorldPopupLabel> _aliveWorldLabels = new();
    private readonly Dictionary<CursorPopupData, CursorPopupLabel> _aliveCursorLabels = new();

    /// <summary>
    /// List of popups that have been predicted by the client.
    /// If a popup is received from the server that matches one of these, it will be ignored to prevent duplicates.
    /// </summary>
    private readonly List<IPopupPredictionInstance> _predictionInstances = new();

    public const float MinimumPopupLifetime = 0.7f;
    public const float MaximumPopupLifetime = 5f;
    public const float PopupLifetimePerCharacter = 0.04f;

    // WD EDIT START - Log actions in chat
    private static readonly Dictionary<PopupType, string> FontSizeDict = new()
    {
        { PopupType.Medium, "12" },
        { PopupType.MediumCaution, "12" },
        { PopupType.Large, "15" },
        { PopupType.LargeCaution, "15" }
    };

    private bool _shouldLogInChat;
    // WD EDIT END

    public override void Initialize()
    {
        base.Initialize();

        // WD EDIT START - Log actions in chat
        Subs.CVar(_configManager, GoobCVars.LogInChat, log => _shouldLogInChat = log, true);
        // WD EDIT END

        _overlay.AddOverlay(new PopupOverlay(
            _configManager,
            EntityManager,
            _playerManager,
            ProtoMan,
            _uiManager,
            _uiManager.GetUIController<PopupUIController>(),
            _examine,
            _transform,
            this));
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlay.RemoveOverlay<PopupOverlay>();
    }

    /// <summary>
    /// If the same popup is repeated, this will make show x2, x3, x4, ... at the end of the message instead of creating a new, overlapping popup.
    /// </summary>
    private void WrapAndRepeatPopup(PopupLabel existingLabel, string popupMessage)
    {
        existingLabel.TotalTime = 0;
        existingLabel.Repeats += 1;
        existingLabel.Text = Loc.GetString("popup-system-repeated-popup-stacking-wrap",
            ("popup-message", popupMessage),
            ("count", existingLabel.Repeats));
    }

    /// <summary>
    /// Interal implementation for both coordinates and entity popups.
    /// </summary>
    private void PopupInternal(string? message, PopupType type, EntityCoordinates coordinates, EntityUid? entity, bool recordReplay)
    {
        if (message == null)
            return;

        if (recordReplay && _replayRecording.IsRecording)
        {
            if (entity != null)
                _replayRecording.RecordClientMessage(new PopupEntityEvent(message, type, Timing.CurTick, GetNetEntity(entity.Value)));
            else
                _replayRecording.RecordClientMessage(new PopupCoordinatesEvent(message, type, Timing.CurTick, GetNetCoordinates(coordinates), 0));
        }

        var popupData = new WorldPopupData(message, type, coordinates, entity);
        if (_aliveWorldLabels.TryGetValue(popupData, out var existingLabel))
        {
            // WD EDIT - Log actions in chat: intended dedup. A repeat of a still-alive popup stacks on the
            // world label (x2/x3...) and returns here BEFORE the chat-logging block below, so spammed identical
            // actions are logged to chat only once instead of flooding it. This is deliberate, not a missed case.
            WrapAndRepeatPopup(existingLabel, popupData.Message);
            return;
        }

        var label = new WorldPopupLabel(coordinates)
        {
            Text = message,
            Type = type,
        };

        _aliveWorldLabels.Add(popupData, label);

        // WD EDIT START - Log actions in chat
        // Only reached for a NEW world label (first occurrence); repeats short-circuit above by design, so chat
        // gets one line per distinct popup rather than one per repeat.
        if (_shouldLogInChat &&
            _playerManager.LocalEntity != null &&
            coordinates.IsValid(EntityManager) &&
            _examine.InRangeUnOccluded(_playerManager.LocalEntity.Value, coordinates, 10))
        {
            var fontsize = FontSizeDict.GetValueOrDefault(type, "10");
            var fontcolor = type is PopupType.LargeCaution or PopupType.MediumCaution or PopupType.SmallCaution
                ? "#C62828"
                : "#AEABC4";

            var escapedMessage = FormattedMessage.EscapeText(message);
            var wrappedMessage = $"[font size={fontsize}][color={fontcolor}]{escapedMessage}[/color][/font]";
            var chatMsg = new ChatMessage(ChatChannel.Popup, message, wrappedMessage, GetNetEntity(EntityUid.Invalid), null);
            _uiManager.GetUIController<ChatUIController>().ProcessChatMessage(chatMsg);
        }
        // WD EDIT END
    }

    /// <summary>
    /// Internal implementation for cursor popups.
    /// </summary>
    private void PopupCursorInternal(string? message, PopupType type, bool recordReplay)
    {
        if (message == null)
            return;

        if (recordReplay && _replayRecording.IsRecording)
            _replayRecording.RecordClientMessage(new PopupCursorEvent(message, type, Timing.CurTick));

        var popupData = new CursorPopupData(message, type);
        if (_aliveCursorLabels.TryGetValue(popupData, out var existingLabel))
        {
            WrapAndRepeatPopup(existingLabel, popupData.Message);
            return;
        }

        var label = new CursorPopupLabel(_inputManager.MouseScreenPosition)
        {
            Text = message,
            Type = type,
        };

        _aliveCursorLabels.Add(popupData, label);
    }

    #region Abstract Method Implementations

    /// <summary>
    /// Shows a popup at the local user's cursor.
    /// </summary>
    /// <remarks>
    /// This overload only exists on the client. If you want to use this in Shared you should use the overload that takes a recipient, session or filter instead.
    /// We do not add a virtual method to the shared system because that will cause problems if the shared code is run in a non-predicted way.
    /// </remarks>
    /// <param name="message">The message to display.</param>
    /// <param name="type">Used to customize how this popup should appear visually.</param>
    public void PopupCursor(string? message, PopupType type = PopupType.Small)
    {
        if (!Timing.IsFirstTimePredicted || message is null)
            return;

        _predictionInstances.Add(new PopupCursorEvent.PredictionInstance(message, type, Timing.CurTick));
        PopupCursorInternal(message, type, true);
    }

    public override void PopupCursor(string? message, EntityUid? recipient, PopupType type = PopupType.Small)
    {
        if (_playerManager.LocalEntity == recipient)
            PopupCursor(message, type);
    }

    public override void PopupCursor(string? message, ICommonSession recipient, PopupType type = PopupType.Small)
    {
        if (_playerManager.LocalSession == recipient)
            PopupCursor(message, type);
    }

    public override void PopupCursor(string? message, Filter filter, bool recordReplay, PopupType type = PopupType.Small)
    {
        if (filter.Recipients.Contains(_playerManager.LocalSession))
            PopupCursor(message, type);
    }

    public override void PopupCoordinates(string? message, EntityCoordinates coordinates, PopupType type = PopupType.Small, int predictionKey = 0)
    {
        if (!Timing.IsFirstTimePredicted || message is null)
            return;

        _predictionInstances.Add(new PopupCoordinatesEvent.PredictionInstance(message, type, Timing.CurTick, predictionKey));
        PopupInternal(message, type, coordinates, null, true);
    }

    public override void PopupCoordinates(string? message, EntityCoordinates coordinates, ICommonSession recipient, PopupType type = PopupType.Small, int predictionKey = 0)
    {
        if (_playerManager.LocalSession == recipient)
            PopupCoordinates(message, coordinates, type, predictionKey);
    }

    public override void PopupCoordinates(string? message, EntityCoordinates coordinates, EntityUid? recipient, PopupType type = PopupType.Small, int predictionKey = 0)
    {
        if (_playerManager.LocalEntity == recipient)
            PopupCoordinates(message, coordinates, type, predictionKey);
    }

    public override void PopupCoordinates(string? message, EntityCoordinates coordinates, Filter filter, bool replayRecord, PopupType type = PopupType.Small, int predictionKey = 0)
    {
        if (filter.Recipients.Contains(_playerManager.LocalSession))
            PopupCoordinates(message, coordinates, type, predictionKey);
    }

    public override void PopupEntity(string? message, EntityUid uid, PopupType type = PopupType.Small)
    {
        if (!Timing.IsFirstTimePredicted || message is null)
            return;

        if (!TryComp(uid, out TransformComponent? transform))
            return;

        _predictionInstances.Add(new PopupEntityEvent.PredictionInstance(message, type, Timing.CurTick, GetNetEntity(uid)));
        PopupInternal(message, type, transform.Coordinates, uid, true);
    }

    public override void PopupEntity(string? message, EntityUid uid, EntityUid? recipient, PopupType type = PopupType.Small)
    {
        if (_playerManager.LocalEntity == recipient)
            PopupEntity(message, uid, type);
    }

    public override void PopupEntity(string? message, EntityUid uid, ICommonSession recipient, PopupType type = PopupType.Small)
    {
        if (_playerManager.LocalSession == recipient)
            PopupEntity(message, uid, type);
    }

    public override void PopupEntity(string? message, EntityUid uid, Filter filter, bool recordReplay, PopupType type = PopupType.Small)
    {
        if (filter.Recipients.Contains(_playerManager.LocalSession))
            PopupEntity(message, uid, type);
    }

    #endregion

    #region Network Event Handlers

    [SubscribeNetworkEvent]
    private void OnPopupCursorEvent(PopupCursorEvent ev)
    {
        var instance = new PopupCursorEvent.PredictionInstance(ev.Message, ev.Type, ev.Tick);
        if (_predictionInstances.Remove(instance))
            return;

        PopupCursorInternal(ev.Message, ev.Type, false);
    }

    [SubscribeNetworkEvent]
    private void OnPopupCoordinatesEvent(PopupCoordinatesEvent ev)
    {
        var instance = new PopupCoordinatesEvent.PredictionInstance(ev.Message, ev.Type, ev.Tick, ev.PredictionKey);
        if (_predictionInstances.Remove(instance))
            return;

        PopupInternal(ev.Message, ev.Type, GetCoordinates(ev.Coordinates), null, false);
    }

    [SubscribeNetworkEvent]
    private void OnPopupEntityEvent(PopupEntityEvent ev)
    {
        var instance = new PopupEntityEvent.PredictionInstance(ev.Message, ev.Type, ev.Tick, ev.Uid);
        if (_predictionInstances.Remove(instance))
            return;

        var entity = GetEntity(ev.Uid);

        if (TryComp(entity, out TransformComponent? transform))
            PopupInternal(ev.Message, ev.Type, transform.Coordinates, entity, false);
    }

    [SubscribeNetworkEvent]
    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _aliveCursorLabels.Clear();
        _aliveWorldLabels.Clear();
        _predictionInstances.Clear();
    }

    #endregion

    /// <summary>
    /// Calculates the lifetime of a popup based on its text length.
    /// </summary>
    public static float GetPopupLifetime(PopupLabel label)
    {
        return Math.Clamp(PopupLifetimePerCharacter * label.Text.Length,
            MinimumPopupLifetime,
            MaximumPopupLifetime);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!Timing.IsFirstTimePredicted)
            return; // We only need to clean up once per tick.


        // We only keep track of prediction instances for a short amount of time to prevent memory leaks.
        // We can safely assume that if a popup hasn't been received from the server within a 10 seconds, it will never be received.
        var deleteTickCount = Timing.TickRate * 10;
        if (_predictionInstances.Count != 0)
        {
            _predictionInstances.RemoveAll(p => (int)Timing.CurTick.Value - (int)p.Tick.Value > deleteTickCount);
        }
    }

    public override void FrameUpdate(float frameTime)
    {
        if (_aliveWorldLabels.Count == 0 && _aliveCursorLabels.Count == 0)
            return;

        if (_aliveWorldLabels.Count > 0)
        {
            var aliveWorldToRemove = new ValueList<WorldPopupData>();
            foreach (var (data, label) in _aliveWorldLabels)
            {
                label.TotalTime += frameTime;
                if (label.TotalTime > GetPopupLifetime(label) || Deleted(label.InitialPos.EntityId))
                {
                    aliveWorldToRemove.Add(data);
                }
            }
            foreach (var data in aliveWorldToRemove)
            {
                _aliveWorldLabels.Remove(data);
            }
        }

        if (_aliveCursorLabels.Count > 0)
        {
            var aliveCursorToRemove = new ValueList<CursorPopupData>();
            foreach (var (data, label) in _aliveCursorLabels)
            {
                label.TotalTime += frameTime;
                if (label.TotalTime > GetPopupLifetime(label))
                {
                    aliveCursorToRemove.Add(data);
                }
            }
            foreach (var data in aliveCursorToRemove)
            {
                _aliveCursorLabels.Remove(data);
            }
        }
    }

    public abstract class PopupLabel
    {
        public PopupType Type = PopupType.Small;
        public string Text { get; set; } = string.Empty;
        public float TotalTime { get; set; }
        public int Repeats = 1;
    }

    public sealed class WorldPopupLabel(EntityCoordinates coordinates) : PopupLabel
    {
        /// <summary>
        /// The original EntityCoordinates of the label.
        /// </summary>
        public EntityCoordinates InitialPos = coordinates;
    }

    public sealed class CursorPopupLabel(ScreenCoordinates screenCoords) : PopupLabel
    {
        public ScreenCoordinates InitialPos = screenCoords;
    }

    [UsedImplicitly]
    private record struct WorldPopupData(
        string Message,
        PopupType Type,
        EntityCoordinates Coordinates,
        EntityUid? Entity);

    [UsedImplicitly]
    private record struct CursorPopupData(
        string Message,
        PopupType Type);
}
