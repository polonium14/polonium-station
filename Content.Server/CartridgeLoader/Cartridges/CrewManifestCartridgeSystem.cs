// SPDX-FileCopyrightText: 2023 Phill101 <28949487+Phill101@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 Phill101 <holypics4@gmail.com>
// SPDX-FileCopyrightText: 2023 Vordenburg <114301317+Vordenburg@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Tayrtahn <tayrtahn@gmail.com>
// SPDX-FileCopyrightText: 2026 Pieter-Jan Briers <pieterjan.briers+git@gmail.com>
// SPDX-FileCopyrightText: 2026 Schuyler Duryee <skywd7766@gmail.com>
// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 pathetic meowmeow <uhhadd@gmail.com>
// SPDX-FileCopyrightText: 2026 taydeo <tay@funkystation.org>
// SPDX-FileCopyrightText: 2026 taydeo <td12233a@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.CrewManifest;
using Content.Server.Station.Systems;
using Content.Shared.CartridgeLoader;
using Content.Shared.CartridgeLoader.Cartridges;
using Content.Shared.CCVar;
using Content.Shared.CrewManifest;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Server.CartridgeLoader.Cartridges;

public sealed partial class CrewManifestCartridgeSystem : EntitySystem
{
    [Dependency] private CartridgeLoaderSystem _cartridgeLoader = default!;
    [Dependency] private IConfigurationManager _configManager = default!;
    [Dependency] private CrewManifestSystem _crewManifest = default!;
    [Dependency] private StationSystem _stationSystem = default!;

    private static readonly EntProtoId CartridgePrototypeName = "CrewManifestCartridge";

    /// <summary>
    /// Flag that shows that if crew manifest is allowed to be viewed from 'unsecure' entities,
    /// which is the keys for the cartridge.
    /// </summary>
    private bool _unsecureViewersAllowed = true;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CrewManifestCartridgeComponent, CartridgeMessageEvent>(OnUiMessage);
        SubscribeLocalEvent<CrewManifestCartridgeComponent, CartridgeUiReadyEvent>(OnUiReady);
        SubscribeLocalEvent<ProgramInstallationAttempt>(OnInstallationAttempt);
        Subs.CVar(_configManager, CCVars.CrewManifestUnsecure, OnCrewManifestUnsecureChanged, true);
    }

    /// <summary>
    /// The ui messages received here get wrapped by a CartridgeMessageEvent and are relayed from the <see cref="CartridgeLoaderSystem"/>
    /// </summary>
    /// <remarks>
    /// The cartridge specific ui message event needs to inherit from the CartridgeMessageEvent
    /// </remarks>
    private void OnUiMessage(EntityUid uid, CrewManifestCartridgeComponent component, CartridgeMessageEvent args)
    {
        UpdateUiState(uid, GetEntity(args.LoaderUid), component);
    }

    /// <summary>
    /// This gets called when the ui fragment needs to be updated for the first time after activating
    /// </summary>
    private void OnUiReady(EntityUid uid, CrewManifestCartridgeComponent component, CartridgeUiReadyEvent args)
    {
        UpdateUiState(uid, args.Loader, component);
    }

    private void UpdateUiState(EntityUid uid, EntityUid loaderUid, CrewManifestCartridgeComponent? component)
    {
        if (!Resolve(uid, ref component))
            return;

        var owningStation = _stationSystem.GetOwningStation(uid);

        if (owningStation is null)
        {
            // Display "loading failed" message
            var failureMessage = Loc.GetString("crew-manifest-cartridge-loading-failed");
            var failureState = new CrewManifestUiState(failureMessage, new CrewManifestEntries());
            _cartridgeLoader.UpdateCartridgeUiState(loaderUid, failureState);
            return;
        }

        var (stationName, entries) = _crewManifest.GetCrewManifest(owningStation.Value);

        var state = new CrewManifestUiState(stationName, entries);
        _cartridgeLoader.UpdateCartridgeUiState(loaderUid, state);
    }

    private void OnInstallationAttempt(ref ProgramInstallationAttempt args)
    {
        if (args.Prototype == CartridgePrototypeName && !_unsecureViewersAllowed)
            args.Cancelled = true;
    }

    private void OnCrewManifestUnsecureChanged(bool unsecureViewersAllowed)
    {
        _unsecureViewersAllowed = unsecureViewersAllowed;

        var allCartridgeLoaders = AllEntityQuery<CartridgeLoaderComponent>();
        while (allCartridgeLoaders.MoveNext(out var loaderUid, out var comp))
        {
            if (_unsecureViewersAllowed)
            {
                if (!_cartridgeLoader.HasProgram<CrewManifestCartridgeComponent>((loaderUid, comp)))
                    _cartridgeLoader.InstallProgram((loaderUid, comp), CartridgePrototypeName, false);

                continue;
            }

            if (_cartridgeLoader.TryGetProgram<CrewManifestCartridgeComponent>((loaderUid, comp)) is { } program)
                _cartridgeLoader.UninstallProgram((loaderUid, comp), program);
        }
    }
}
