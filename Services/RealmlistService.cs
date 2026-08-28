using System.IO;

namespace PoopCraftLauncher.Services;

/// <summary>
/// Keeps realmlist.wtf pointed at the configured server. Vanilla clients read this
/// once at login-screen load, so a stale value (e.g. left on 127.0.0.1 after testing)
/// is the single most common "can't connect" cause — fix it before anything else runs.
/// </summary>
public class RealmlistService
{
    public static void EnsureRealmlist(string clientPath, string realmAddress)
    {
        var path = Path.Combine(clientPath, "realmlist.wtf");
        var desired = $"set realmlist {realmAddress}";

        if (File.Exists(path))
        {
            var current = File.ReadAllText(path).Trim();
            if (string.Equals(current, desired, StringComparison.OrdinalIgnoreCase))
                return;
        }

        File.WriteAllText(path, desired + Environment.NewLine);
    }
}
