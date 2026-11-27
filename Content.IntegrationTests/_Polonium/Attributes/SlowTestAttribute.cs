using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

namespace Content.IntegrationTests._Polonium.Attributes;

/// <summary>
/// Skips the test unless <see cref="Variable"/> is "1". Applied when the test is built, so a skipped test never
/// borrows a pair or starts a round. PR CI leaves the variable unset; test-event-scheduler.yml sets it.
/// A VSTest --filter is not enough: the NUnit adapter cannot translate a purely negative filter, runs every
/// test and only hides the excluded ones from the summary.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class SlowTestAttribute : NUnitAttribute, IApplyToTest
{
    public const string Variable = "POLONIUM_SLOW_TESTS";

    public void ApplyToTest(Test test)
    {
        if (test.RunState == RunState.NotRunnable || Environment.GetEnvironmentVariable(Variable) == "1")
            return;

        test.RunState = RunState.Ignored;
        test.Properties.Set(PropertyNames.SkipReason, $"Slow test, runs only with {Variable}=1 (nightly CI).");
    }
}
