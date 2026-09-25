using Content.Shared._Polonium.NewLife;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Client._Polonium.NewLife;

public sealed partial class NewLifeSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _player = default!;

    private readonly HashSet<string> _previousCharacters = new(StringComparer.OrdinalIgnoreCase);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<NewLifePreviousCharactersEvent>(OnPreviousCharacters);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }


    public bool TryGetRemaining(out TimeSpan remaining)
    {
        remaining = TimeSpan.Zero;

        if (!_cfg.GetCVar(CCVars.NewLifeEnabled))
            return false;

        if (!TryComp<NewLifeComponent>(_player.LocalEntity, out var comp))
            return false;

        var max = _cfg.GetCVar(CCVars.NewLifeMaxNewLives);
        if (max > 0 && comp.UsedLives >= max)
            return false;

        var availableAt = comp.GhostedAt + TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.NewLifeDelay));
        remaining = availableAt - _timing.CurTime;
        if (remaining < TimeSpan.Zero)
            remaining = TimeSpan.Zero;

        return true;
    }

    public int? GetLivesLeft()
    {
        var max = _cfg.GetCVar(CCVars.NewLifeMaxNewLives);
        if (max <= 0)
            return null;

        var used = TryComp<NewLifeComponent>(_player.LocalEntity, out var comp) ? comp.UsedLives : 0;
        return Math.Max(0, max - used);
    }

    public void RequestNewLife()
    {
        RaiseNetworkEvent(new NewLifeRequestEvent());
    }

    public bool IsPreviousCharacter(string? name)
    {
        return !string.IsNullOrWhiteSpace(name) && _previousCharacters.Contains(name.Trim());
    }

    private void OnPreviousCharacters(NewLifePreviousCharactersEvent ev)
    {
        _previousCharacters.Clear();
        foreach (var name in ev.Names)
        {
            _previousCharacters.Add(name.Trim());
        }
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _previousCharacters.Clear();
    }
}
