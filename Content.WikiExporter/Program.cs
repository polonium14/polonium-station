using System;
using System.IO;
using System.Threading.Tasks;
using Content.IntegrationTests;
using Robust.UnitTesting.Pool;

namespace Content.WikiExporter;

/// <summary>
/// Starts a game server from the integration test pool and writes the chemistry data for wiki based on Wiki.JS.
/// </summary>
internal static class Program
{
    private static readonly ExternalTestContext TestContext = new("Wiki exporter", StreamWriter.Null);

    private static async Task<int> Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine("Usage: Content.WikiExporter <output.json>");
            return 1;
        }

        PoolManager.Startup();
        try
        {
            var pair = await PoolManager.GetServerClient(new PoolSettings { NoLoadTestPrototypes = true }, TestContext);
            try
            {
                var server = pair.Server;
                var json = string.Empty;
                await server.WaitPost(() => json = new ChemistryExporter(server.InstanceDependencyCollection).Export());
                var output = Path.GetFullPath(args[0]);
                Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                await File.WriteAllTextAsync(output, json);
            }
            finally
            {
                await pair.CleanReturnAsync();
            }
        }
        finally
        {
            PoolManager.Shutdown();
        }

        return 0;
    }
}
