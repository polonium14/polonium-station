#nullable enable
using System.Collections.Generic;
using System.Linq;
using Robust.Shared.Log;
using Serilog.Events;

namespace Content.IntegrationTests._Polonium.Pair;

/// <summary>
/// Re-logs "Unknown messageId" warnings of the localization manager as errors.
/// </summary>
public sealed class MissingLocLogHandler(ISawmill failures, bool reportLive, Func<string?, bool> ignore) : ILogHandler
{
    private readonly List<string> _startup = new();
    private bool _armed;

    public static bool IsMissingLoc(LogEvent message)
    {
        return message.Level == LogEventLevel.Warning
               && message.MessageTemplate.Text.StartsWith("Unknown messageId", StringComparison.Ordinal);
    }

    public static string? MessageId(LogEvent message)
    {
        return message.Properties.TryGetValue("messageId", out var value) && value is ScalarValue { Value: string id }
            ? id
            : null;
    }

    public void Log(string sawmillName, LogEvent message)
    {
        if (!IsMissingLoc(message) || ignore(MessageId(message)))
            return;

        lock (_startup)
        {
            if (!_armed)
            {
                _startup.Add(message.RenderMessage());
                return;
            }
        }

        if (reportLive)
            failures.Error(message.RenderMessage());
    }

    /// <summary>
    /// Reports the warnings logged during startup, before the test pair could treat errors as failures.
    /// </summary>
    public void Arm()
    {
        List<string> startup;
        lock (_startup)
        {
            _armed = true;
            startup = _startup.Distinct().ToList();
            _startup.Clear();
        }

        foreach (var message in startup)
        {
            failures.Error($"During startup: {message}");
        }
    }
}
