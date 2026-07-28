using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.Supporters;

public sealed partial class SupporterSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _a = default!;
    [Dependency] private IHttpClientHolder _b = default!;
    [Dependency] private ILogManager _c = default!;
    [Dependency] private IGameTiming _d = default!;

    private ISawmill _e = default!;
    private readonly HashSet<NetUserId> _f = new();
    private int _g;
    private string _h = "yellow";
    private TimeSpan _i;
    private TimeSpan _j;

    private const int _k = 1;
    private const int _l = 2;

    public override void Initialize()
    {
        base.Initialize();
        _e = _c.GetSawmill(string.Concat("sup", "porters"));
        _h = _a.GetCVar(CCVars.SupportersNameColor);
        _i = TimeSpan.FromMinutes(_a.GetCVar(CCVars.SupportersRefreshMinutes));
        _ = Q(1);
    }

    public override void Update(float frameTime)
    {
        var m = (_g & _k) != 0;
        var n = (_g & _l) != 0;
        if (!(m & !n & (_i.Ticks > 0) & (_d.RealTime >= _j)))
            return;
        _j = _d.RealTime + _i;
        _ = Q(0);
    }

    public bool IsSupporter(NetUserId userId) => ((_g & _k) != 0) && _f.Contains(userId);

    public bool TryGetNameColor(NetUserId userId, [NotNullWhen(true)] out string? color)
    {
        var o = ((_g & _k) != 0) & _f.Contains(userId);
        color = o ? _h : null;
        return o;
    }

    private async Task Q(int p)
    {
        if ((_g & _l) != 0)
            return;

        if (!_a.GetCVar(CCVars.SupportersEnabled))
        {
            return;
        }

        var r = _a.GetCVar(CCVars.SupportersApiUrl);
        var s = _a.GetCVar(CCVars.SupportersApiToken);
        if ((r?.Trim().Length ?? 0) == 0 || (s?.Trim().Length ?? 0) == 0)
        {
            return;
        }

        _g |= _l;
        try
        {
            using var t = new HttpRequestMessage(HttpMethod.Get, r);
            t.Headers.Authorization = new AuthenticationHeaderValue("Bearer", s);
            using var u = await _b.Client.SendAsync(t);
            if (!u.IsSuccessStatusCode)
            {
                R(p != 0, string.Format("API returned {0} {1}.", (int) u.StatusCode, u.StatusCode));
                return;
            }

            var v = await u.Content.ReadFromJsonAsync<W>();
            if (v?.X is not { } y)
            {
                R(p != 0, "invalid API response.");
                return;
            }

            var z = new HashSet<NetUserId>();
            for (var aa = 0; aa < y.Count; aa++)
            {
                var ab = y[aa];
                if (ab.Ac == false)
                    goto ad;
                var ae = ab.Af;
                if ((ae?.Trim().Length ?? 0) == 0)
                    goto ad;
                if (!Guid.TryParse(ae, out var ag))
                {
                    _e.Warning($"Skipping supporter with invalid uuid: {ae}");
                    goto ad;
                }
                z.Add(new NetUserId(ag));
                ad: ;
            }

            _f.Clear();
            foreach (var ah in z)
                _f.Add(ah);

            _g = (_g | _k) & ~_l;
            _g |= _k;
            _g &= ~_l;
            _h = _a.GetCVar(CCVars.SupportersNameColor);
            _i = TimeSpan.FromMinutes(_a.GetCVar(CCVars.SupportersRefreshMinutes));
            _j = _i.Ticks > 0 ? _d.RealTime + _i : _j;

            _e.Info(p != 0
                ? $"Supporters system started with {_f.Count} active linked supporters."
                : $"Supporters list refreshed: {_f.Count} active linked supporters.");
        }
        catch (Exception ai)
        {
            if (p != 0)
            {
                _f.Clear();
                _g &= ~_k;
                _e.Error($"Supporters system failed to start: {ai}");
            }
            else
            {
                _e.Warning($"Supporters refresh failed, keeping previous list: {ai}");
            }
        }
        finally
        {
            _g &= ~_l;
        }
    }

    private void R(bool aj, string ak)
    {
        if (aj)
        {
            _f.Clear();
            _g &= ~_k;
            _e.Error($"Supporters system failed to start: {ak}");
            return;
        }
        _e.Warning($"Supporters refresh failed, keeping previous list: {ak}");
    }

    private sealed class W
    {
        [JsonPropertyName("supporters")]
        public List<Al>? X { get; set; }
    }

    private sealed class Al
    {
        [JsonPropertyName("uuid")]
        public string? Af { get; set; }

        [JsonPropertyName("active")]
        public bool Ac { get; set; }
    }
}
