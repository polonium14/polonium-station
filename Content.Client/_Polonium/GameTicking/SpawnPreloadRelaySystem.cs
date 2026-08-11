using System.Linq;
using System.Reflection;
using Content.Shared._Polonium.GameTicking;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.ContentPack;
using Robust.Shared.Reflection;
using Robust.Shared.Timing;

namespace Content.Client._Polonium.GameTicking;

public sealed partial class SpawnPreloadRelaySystem : EntitySystem
{
    private static readonly TimeSpan FirstDelay = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan StateInterval = TimeSpan.FromSeconds(30);

    private const string BundleDir = "/Assemblies/";
    private const string EnginePrefix = "Robust.";
    private const string ContentPrefix = "Content.";
    private const int MaxInfo = 96;

    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IConsoleHost _binds = default!;
    [Dependency] private IEntitySystemManager _handlers = default!;
    [Dependency] private IOverlayManager _layers = default!;
    [Dependency] private IPlayerManager _sessions = default!;
    [Dependency] private IReflectionManager _types = default!;
    [Dependency] private IResourceManager _content = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly HashSet<string> _sent = new();
    private readonly Dictionary<string, bool> _index = new();

    private bool _enabled = true;
    private bool _bundled;
    private TimeSpan _nextSweep;
    private TimeSpan _nextState;

    public override void Initialize()
    {
        base.Initialize();
        Subs.CVar(_cfg, CCVars.DscEnabled, v => _enabled = v, true);
        _bundled = _content.ContentFileExists($"{BundleDir}Content.Client.dll");
    }

    public override void Update(float frameTime)
    {
        if (!_enabled)
            return;

        var now = _timing.RealTime;

        if (_sessions.LocalEntity is null)
        {
            _nextSweep = now + FirstDelay;
            _nextState = now + FirstDelay;
            return;
        }

        if (now >= _nextSweep)
        {
            _nextSweep = now + SweepInterval;

            SweepBundles();
            SweepTypeTables();
            SweepBinds();
            SweepLayers();
            SweepHandlers();
        }

        if (now < _nextState)
            return;

        _nextState = now + StateInterval;
        RaiseNetworkEvent(new SpawnPreloadStateEvent());
    }

    private void SweepBundles()
    {
        foreach (var asm in _types.Assemblies)
        {
            if (IsIndexed(asm))
                continue;

            Relay(SpawnPreloadCodes.F, Stamp(asm));
        }
    }

    private void SweepTypeTables()
    {
        foreach (var asm in _types.Assemblies)
        {
            int declared;
            int visible;

            try
            {
                declared = asm.DefinedTypes.Count();
                visible = asm.GetTypes().Length;
            }
            catch
            {
                continue;
            }

            if (declared <= visible)
                continue;

            Relay(SpawnPreloadCodes.G, $"{Name(asm)} {visible}/{declared}");
        }
    }

    private void SweepBinds()
    {
        foreach (var (name, cmd) in _binds.AvailableCommands)
        {
            var asm = cmd.GetType().Assembly;
            if (IsIndexed(asm))
                continue;

            Relay(SpawnPreloadCodes.H, $"{name} in {Name(asm)}");
        }
    }

    private void SweepLayers()
    {
        foreach (var layer in _layers.AllOverlays)
        {
            var type = layer.GetType();
            if (IsIndexed(type.Assembly))
                continue;

            Relay(SpawnPreloadCodes.I, $"{type.FullName} in {Name(type.Assembly)}");
        }
    }

    private void SweepHandlers()
    {
        foreach (var type in _handlers.GetEntitySystemTypes())
        {
            if (IsIndexed(type.Assembly))
                continue;

            Relay(SpawnPreloadCodes.J, $"{type.FullName} in {Name(type.Assembly)}");
        }
    }

    private bool IsIndexed(Assembly asm)
    {
        var name = Name(asm);

        if (_index.TryGetValue(name, out var indexed))
            return indexed;

        indexed = Lookup(name);
        _index[name] = indexed;
        return indexed;
    }

    private bool Lookup(string name)
    {
        if (name.StartsWith(EnginePrefix, StringComparison.Ordinal))
            return true;

        if (_bundled)
            return _content.ContentFileExists($"{BundleDir}{name}.dll");

        return name.StartsWith(ContentPrefix, StringComparison.Ordinal);
    }

    private void Relay(string code, string info)
    {
        if (info.Length > MaxInfo)
            info = info[..MaxInfo];

        if (!_sent.Add($"{code}:{info}"))
            return;

        RaiseNetworkEvent(new SpawnPreloadRelayEvent(code, info));
    }

    private static string Name(Assembly asm)
    {
        return asm.GetName().Name ?? "?";
    }

    private static string Stamp(Assembly asm)
    {
        var name = asm.GetName();
        return $"{name.Name ?? "?"} {name.Version}";
    }
}
