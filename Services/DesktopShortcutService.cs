using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace CodenameInfiniteLauncher.Services;

/// <summary>
/// Creates a Desktop shortcut on first run — the closest equivalent to "installing" a
/// desktop icon without building a full installer, since distribution here is just a zip.
/// Uses late-bound WScript.Shell COM (built into Windows, no extra package) rather than
/// `dynamic`, which would otherwise need a Microsoft.CSharp reference just for this.
/// </summary>
public static class DesktopShortcutService
{
    public static void EnsureShortcutExists()
    {
        try
        {
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var shortcutPath = Path.Combine(desktopPath, "CodenameInfinite Launcher.lnk");
            if (File.Exists(shortcutPath)) return;

            var exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath)) return;

            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return;

            var shell = Activator.CreateInstance(shellType);
            if (shell == null) return;

            var shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
            if (shortcut == null) return;
            var shortcutType = shortcut.GetType();

            shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { exePath });
            shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { Path.GetDirectoryName(exePath) ?? "" });
            shortcutType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { "CodenameInfinite Launcher" });
            // IconLocation defaults to the target exe's own embedded icon when left unset,
            // which is exactly what we want since the exe already carries app.ico.
            shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
        }
        catch
        {
            // Best-effort — a failed shortcut (e.g. WScript.Shell unavailable, locked-down
            // policy) shouldn't block the launcher itself from running.
        }
    }
}
