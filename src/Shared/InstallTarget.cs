using System.Diagnostics;
using System.Text.Json;
namespace DivineLabs.IdleSlayer.Maintenance;

public enum InstallTargetKind { Empty, Installed, Recoverable, Occupied }

public static class InstallTarget
{
    public const string ProductId = "DivineLabs.IdleSlayer";
    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Programs", "DivineLabs", "Idle Slayer Mods");

    public static InstallTargetKind Inspect(string root)
    {
        if (!Directory.Exists(root) || !Directory.EnumerateFileSystemEntries(root).Any())
            return InstallTargetKind.Empty;
        if (HasOwner(root, "Data/Config/.divinelabs-owner") || HasOwner(root, ".divinelabs-owner") ||
            File.Exists(Path.Combine(root, "DivineLabs.IdleSlayer.exe")) ||
            File.Exists(Path.Combine(root, "App/Setup/installation.json")) ||
            File.Exists(Path.Combine(root, "installation.json"))) return InstallTargetKind.Installed;
        return LegacyFailure(root) ? InstallTargetKind.Recoverable : InstallTargetKind.Occupied;
    }

    private static bool HasOwner(string root, string name)
    {
        string file = Path.Combine(root, name);
        return File.Exists(file) && File.ReadAllText(file) == ProductId;
    }

    private static bool LegacyFailure(string root)
    {
        // 0.4.0 could leave only its log and extraction stage, without an ownership marker.
        // Recognize that exact shape; never erase or adopt a generic nonempty folder.
        var entries = Directory.GetFileSystemEntries(root);
        if (entries.Length != 1 || Path.GetFileName(entries[0]) != "Data" ||
            (File.GetAttributes(entries[0]) & FileAttributes.ReparsePoint) != 0) return false;
        string data = entries[0], logs = Path.Combine(data, "Logs"), cache = Path.Combine(data, "Cache");
        foreach (string entry in Directory.GetFileSystemEntries(data))
            if (Path.GetFileName(entry) is not ("Logs" or "Cache") ||
                (File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0) return false;
        string log = Path.Combine(logs, "setup.log");
        if (!File.Exists(log) || Directory.GetFileSystemEntries(logs).Any(p => p != log)) return false;
        using (var reader = new StreamReader(log))
        {
            var buffer = new char[65536];
            int count = reader.ReadBlock(buffer, 0, buffer.Length);
            if (!new string(buffer, 0, count).Contains("DivineLabs.IdleSlayer.Setup.", StringComparison.Ordinal)) return false;
        }
        if (!Directory.Exists(cache)) return true;
        foreach (string entry in Directory.GetFileSystemEntries(cache))
            if (Path.GetFileName(entry) != "Work" || !Directory.Exists(entry) ||
                (File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0) return false;
        string work = Path.Combine(cache, "Work");
        if (!Directory.Exists(work)) return true;
        foreach (string stage in Directory.GetFileSystemEntries(work))
        {
            string name = Path.GetFileName(stage);
            if (!Directory.Exists(stage) || (File.GetAttributes(stage) & FileAttributes.ReparsePoint) != 0 ||
                !name.StartsWith("setup-", StringComparison.Ordinal) ||
                !Guid.TryParseExact(name[6..], "N", out _)) return false;
            string executable = Path.Combine(stage, "DivineLabs.IdleSlayer.exe");
            string manifest = Path.Combine(stage, "App/Components/manifest.json");
            bool identified = File.Exists(executable) &&
                FileVersionInfo.GetVersionInfo(executable).ProductName?.StartsWith("DivineLabs", StringComparison.Ordinal) == true;
            if (!identified && File.Exists(manifest))
            {
                try
                {
                    using var document = JsonDocument.Parse(File.ReadAllText(manifest));
                    identified = document.RootElement.TryGetProperty("Files", out var files) &&
                        files.TryGetProperty("Mods/DivineLabs.IdleSlayer.Mod.dll", out _);
                }
                catch (JsonException) { return false; }
            }
            if (!identified) return false;
        }
        return true;
    }
}
