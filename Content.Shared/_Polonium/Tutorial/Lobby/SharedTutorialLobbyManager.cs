// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later


// .___  ___.      ___       _______   _______
// |   \/   |     /   \     |       \ |   ____|
// |  \  /  |    /  ^  \    |  .--.  ||  |__
// |  |\/|  |   /  /_\  \   |  |  |  ||   __|
// |  |  |  |  /  _____  \  |  '--'  ||  |____
// |__|  |__| /__/     \__\ |_______/ |_______|
//
// .______   ____    ____
// |   _  \  \   \  /   /
// |  |_)  |  \   \/   /
// |   _  <    \_    _/
// |  |_)  |     |  |
// |______/      |__|
//
// .______     ______    __        ______   .__   __.  __   __    __  .___  ___.
// |   _  \   /  __  \  |  |      /  __  \  |  \ |  | |  | |  |  |  | |   \/   |
// |  |_)  | |  |  |  | |  |     |  |  |  | |   \|  | |  | |  |  |  | |  \  /  |
// |   ___/  |  |  |  | |  |     |  |  |  | |  . `  | |  | |  |  |  | |  |\/|  |
// |  |      |  `--'  | |  `----.|  `--'  | |  |\   | |  | |  `--'  | |  |  |  |
// | _|       \______/  |_______| \______/  |__| \__| |__|  \______/  |__|  |__|


namespace Content.Shared._Polonium.Tutorial.Lobby;

public abstract class SharedTutorialLobbyManager
{
    /// <summary>
    /// Legacy step indicator for the client-side tutorial lobby flow.
    /// </summary>
    // TODO : podobna indykacja postępu wprowadzenia poprzez prototypy dla serwerowej logiki
    public enum ClientsideTutorialLobbyStep
    {
        None,
        WelcomeMessage,
        LobbyOverview,
        CharacterCreation,
        //Guidebook,
        ProceedPrompt,
        //NonClientside, // Used for steps that only exist on the server, so we can still track progress
    }

    public ClientsideTutorialLobbyStep ConvertToLegacyStep(IClientsideNavTutorialStep? step)
    {
        return step?.StepId switch
        {
            "welcome" => ClientsideTutorialLobbyStep.WelcomeMessage,
            "lobby_overview" => ClientsideTutorialLobbyStep.LobbyOverview,
            "character_creation" => ClientsideTutorialLobbyStep.CharacterCreation,
            //"guidebook" => ClientsideTutorialLobbyStep.Guidebook,
            "proceed_prompt" => ClientsideTutorialLobbyStep.ProceedPrompt,
            //"non_clientside" => ClientsideTutorialLobbyStep.NonClientside,
            _ => ClientsideTutorialLobbyStep.None,
        };
    }
}

/// <summary>
/// A single navigable step in the tutorial lobby sequence.
/// </summary>
public interface IClientsideNavTutorialStep
{
    /// <summary>
    /// Unique identifier for this step.
    /// </summary>
    string StepId { get; }

    /// <summary>
    /// Executes the step, displaying overlays and bubbles.
    /// </summary>
    /// <returns>True if step executed successfully, false otherwise.</returns>
    bool Execute();

    /// <summary>
    /// Cleans up this step's UI elements.
    /// </summary>
    void Cleanup();

    /// <summary>
    /// Called when user returns to this step from a later one.
    /// </summary>
    void OnReenter();

    /// <summary>
    /// Validates if this step can be executed in current state.
    /// </summary>
    /// <returns>True if step can be executed.</returns>
    bool CanExecute();
}
