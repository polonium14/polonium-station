using System.Linq;
using Content.Shared._Polonium.Tutorial.Components;
using Content.Shared.Input;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Shared.Input;

namespace Content.Client._Polonium.Tutorial;

/// <summary>
/// Turns the trainee's own keybinds into the keys named in the text. A step never spells a
/// key out itself, so a rebound control reads correctly without touching the locale.
/// </summary>
public sealed partial class TutorialPresentationSystem
{
    private void OnKeybindChanged(IKeyBinding _)
    {
        _keyArgs = null;

        if (_player.LocalEntity is not { } uid || !TryComp<TutorialSessionComponent>(uid, out var session))
            return;

        UpdateHint(session);
    }

    /// <summary>
    /// Every tutorial string gets the whole keybind set. Fluent ignores the args it does not use,
    /// so loc files can drop { $useHand } and friends anywhere without touching this file.
    /// </summary>
    private string FormatTutorialLoc(LocId id)
    {
        return _loc.GetString(id, _keyArgs ??= BuildKeyArgs());
    }

    private (string, object)[] BuildKeyArgs()
    {
        return new (string, object)[]
        {
            ("keys", Paint(FormatMoveKeys())),
            ("move", Paint(FormatMoveKeys())),
            ("walk", PaintKey(EngineKeyFunctions.Walk)),
            ("crawl", PaintKey(ContentKeyFunctions.ToggleKnockdown)),
            ("swap", PaintKey(ContentKeyFunctions.SwapHands)),
            ("drop", PaintKey(ContentKeyFunctions.Drop)),
            ("throwItem", PaintKey(ContentKeyFunctions.ThrowItemInHand)),
            ("useHand", PaintKey(ContentKeyFunctions.UseItemInHand)),
            ("altUseHand", PaintKey(ContentKeyFunctions.AltUseItemInHand)),
            ("useWorld", PaintKey(ContentKeyFunctions.ActivateItemInWorld)),
            ("altUseWorld", PaintKey(ContentKeyFunctions.AltActivateItemInWorld)),
            ("examine", PaintKey(ContentKeyFunctions.ExamineEntity)),
            ("inventory", PaintKey(ContentKeyFunctions.OpenInventoryMenu)),
            ("character", PaintKey(ContentKeyFunctions.OpenCharacterMenu)),
            ("backpack", PaintKey(ContentKeyFunctions.OpenBackpack)),
            ("smartBackpack", PaintKey(ContentKeyFunctions.SmartEquipBackpack)),
            ("smartBelt", PaintKey(ContentKeyFunctions.SmartEquipBelt)),
            ("belt", PaintKey(ContentKeyFunctions.OpenBelt)),
            ("actions", PaintKey(ContentKeyFunctions.OpenActionsMenu)),
            ("craft", PaintKey(ContentKeyFunctions.OpenCraftingMenu)),
            // turns a construction preview before it is placed
            ("rotate", PaintKey(EngineKeyFunctions.EditorRotateObject)),
            ("pull", PaintKey(ContentKeyFunctions.TryPullObject)),
            ("releasePull", PaintKey(ContentKeyFunctions.ReleasePulledObject)),
            ("movePulled", PaintKey(ContentKeyFunctions.MovePulledObject)),
            ("guidebook", PaintKey(ContentKeyFunctions.OpenGuidebook)),
            ("point", PaintKey(ContentKeyFunctions.Point)),
            ("attack", PaintKey(EngineKeyFunctions.Use)),
            ("disarm", PaintKey(EngineKeyFunctions.UseSecondary)),
            ("click", PaintKey(EngineKeyFunctions.Use)),
            ("rightClick", PaintKey(EngineKeyFunctions.UseSecondary)),
            ("escape", PaintKey(EngineKeyFunctions.CloseModals)),
            ("chat", PaintKey(ContentKeyFunctions.FocusChat)),
            ("chatSay", PaintKey(ContentKeyFunctions.FocusLocalChat)),
            ("chatWhisper", PaintKey(ContentKeyFunctions.FocusWhisperChat)),
            ("chatRadio", PaintKey(ContentKeyFunctions.FocusRadio)),
            ("chatEmote", PaintKey(ContentKeyFunctions.FocusEmote)),
            ("chatOoc", PaintKey(ContentKeyFunctions.FocusOOC)),
            ("chatLooc", PaintKey(ContentKeyFunctions.FocusLOOC)),
            ("chatCycle", PaintKey(ContentKeyFunctions.CycleChatChannelForward)),
            // loc files spell these out in full, keep the names they already use
            ("switch-channel-key", PaintKey(ContentKeyFunctions.CycleChatChannelForward)),
            ("default-chat-channel", _loc.GetString("hud-chatbox-select-channel-Local")),
            ("verb-categories-eject", _loc.GetString("verb-categories-eject")),
            ("examine-verb", _loc.GetString("examine-verb-name")),
            ("climb-verb", _loc.GetString("comp-climbable-verb-climb")),
            ("execution-verb", _loc.GetString("execution-verb-name")),
            ("guide-radio", _loc.GetString("guide-entry-radio")),
            ("channel-local", _loc.GetString("hud-chatbox-select-channel-Local")),
            ("channel-whisper", _loc.GetString("hud-chatbox-select-channel-Whisper")),
            ("channel-radio", _loc.GetString("hud-chatbox-select-channel-Radio")),
            ("channel-emote", _loc.GetString("hud-chatbox-select-channel-Emotes")),
            ("channel-ooc", _loc.GetString("hud-chatbox-select-channel-OOC")),
            ("channel-looc", _loc.GetString("hud-chatbox-select-channel-LOOC")),
            ("internals-toggle", _loc.GetString("ent-ActionToggleInternals")),
            ("gas-tank-toggle", _loc.GetString("gas-tank-window-internals-toggle-button")),
            ("tutorial-bubble-acknowledge", _loc.GetString("tutorial-bubble-acknowledge")),
            ("camera", PaintKey(EngineKeyFunctions.CameraRotateRight)),
            ("cameraLeft", PaintKey(EngineKeyFunctions.CameraRotateLeft)),
            ("cameraReset", PaintKey(EngineKeyFunctions.CameraReset)),
            // old loc files used bare {$key}, keep them rendering instead of printing the placeholder
            ("key", PaintKey(ContentKeyFunctions.UseItemInHand)),
            ("hand", PaintKey(ContentKeyFunctions.UseItemInHand)),
            ("world", PaintKey(ContentKeyFunctions.ActivateItemInWorld)),
        };
    }

    private string PaintKey(BoundKeyFunction function)
    {
        return Paint(_input.GetKeyFunctionButtonString(function));
    }

    private static string Paint(string key)
    {
        return $"[color={KeyColor}]{key}[/color]";
    }

    private string Key(BoundKeyFunction function)
    {
        return _input.GetKeyFunctionButtonString(function);
    }

    private string FormatMoveKeys()
    {
        var parts = new[]
        {
            Key(EngineKeyFunctions.MoveUp),
            Key(EngineKeyFunctions.MoveLeft),
            Key(EngineKeyFunctions.MoveDown),
            Key(EngineKeyFunctions.MoveRight),
        };

        if (Array.TrueForAll(parts, static p => p.Length == 1))
            return string.Concat(parts);

        return string.Join(" / ", parts);
    }
}
