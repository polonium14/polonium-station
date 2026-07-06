using Robust.Shared.Configuration;

namespace Content.Client._EE.Supermatter.Consoles;

/// <summary>
/// Access to server CVars for supermatter console display
/// </summary>
internal static class SupermatterConsoleCVars
{
    public static float GetFloat(IConfigurationManager config, CVarDef<float> cvar)
    {
        return config.IsCVarRegistered(cvar.Name) ? config.GetCVar(cvar) : cvar.DefaultValue;
    }
}
