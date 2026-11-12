using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Content.IntegrationTests;
using Robust.UnitTesting.Pool;

namespace Content.WikiExporter;

/// <summary>
/// Starts a game server from the integration test pool and writes data sets for wiki based on Wiki.JS.
/// </summary>
internal static class Program
{
    private static readonly ExternalTestContext TestContext = new("Wiki exporter", StreamWriter.Null);

    /// <summary>
    /// Serializer settings shared by all exporters.
    /// </summary>
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static async Task<int> Main(string[] args)
    {
        var available = typeof(WikiExporter).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(WikiExporter)) && !t.IsAbstract)
            .Select(t => (WikiExporter) Activator.CreateInstance(t)!)
            .ToDictionary(e => e.Name);

        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: Content.WikiExporter <output dir> [data set...]");
            Console.Error.WriteLine($"Data sets: {string.Join(", ", available.Keys.Order())} (all by default)");
            return 1;
        }

        var selected = new List<WikiExporter>();
        foreach (var name in args.Length > 1 ? args[1..] : available.Keys.Order().ToArray())
        {
            if (!available.TryGetValue(name, out var exporter))
            {
                Console.Error.WriteLine($"Unknown data set '{name}'. Available: {string.Join(", ", available.Keys.Order())}");
                return 1;
            }

            selected.Add(exporter);
        }

        var output = Path.GetFullPath(args[0]);
        Directory.CreateDirectory(output);

        PoolManager.Startup();
        try
        {
            var pair = await PoolManager.GetServerClient(new PoolSettings { NoLoadTestPrototypes = true }, TestContext);
            try
            {
                var server = pair.Server;
                foreach (var exporter in selected)
                {
                    var json = string.Empty;
                    await server.WaitPost(() =>
                    {
                        server.InstanceDependencyCollection.InjectDependencies(exporter);
                        json = JsonSerializer.Serialize(exporter.Export(), JsonOptions);
                    });

                    var file = Path.Combine(output, exporter.Name + ".json");
                    await File.WriteAllTextAsync(file, json);
                    Console.WriteLine($"{exporter.Name}: {file}");
                }
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
