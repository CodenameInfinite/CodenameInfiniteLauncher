using System.IO;
using System.Text.Json;
using PoopCraftLauncher.Models;

namespace PoopCraftLauncher.Services;

public static class ConfigService
{
    private static string ConfigPath => Path.Combine(AppContext.BaseDirectory, "config.json");
    private static string ExamplePath => Path.Combine(AppContext.BaseDirectory, "config.example.json");
    private static readonly JsonSerializerOptions WriteOpts = new() { WriteIndented = true };

    public static LauncherConfig Load()
    {
        // Fresh checkout/build: seed a personal config.json from the tracked template so the
        // launcher runs out of the box, without machine-specific paths ever landing in git.
        if (!File.Exists(ConfigPath) && File.Exists(ExamplePath))
            File.Copy(ExamplePath, ConfigPath);

        if (File.Exists(ConfigPath))
        {
            try
            {
                var json = File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize<LauncherConfig>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (config != null) return config;
            }
            catch
            {
                // fall through to default — a corrupt config.json shouldn't crash the launcher
            }
        }

        return new LauncherConfig();
    }

    public static void Save(LauncherConfig config)
    {
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config, WriteOpts));
    }
}
