using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace CopilotEnvSwitcher;

public static class EnvStore
{
    public const string BaseUrl = "COPILOT_PROVIDER_BASE_URL";
    public const string Model = "COPILOT_MODEL";
    public const string ProviderType = "COPILOT_PROVIDER_TYPE";
    public const string ApiKey = "COPILOT_PROVIDER_API_KEY";

    private const string BeginMarker = "# >>> copilot-env-switcher >>>";
    private const string EndMarker = "# <<< copilot-env-switcher <<<";

    private static readonly string Home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    private static readonly string[] ShellProfiles = { ".zshrc", ".bash_profile" };

    public record Option(string Text, string Value)
    {
        public override string ToString() => Text;
    }

    // Lines are "Text,Value"; a line without a comma uses the same string for both.
    public static List<Option> ReadOptions(string file)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Lookups", file);
        if (!File.Exists(path)) return new();
        var lines = File.ReadAllLines(path);
        var items = lines
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .Select(l =>
            {
                var i = l.IndexOf(',');
                return i < 0
                    ? new Option(l, l)
                    : new Option(l[..i].Trim(), l[(i + 1)..].Trim());
            })
            .DistinctBy(o => o.Value)
            .ToList();
        // A blank first line becomes a blank option that clears the variable.
        if (lines.Length > 0 && lines[0].Trim().Length == 0) items.Insert(0, new Option("", ""));
        return items;
    }

    private static string AppliedMarker => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CopilotEnvSwitcher", "applied");

    // True once the user has applied settings, so an empty variable means "chose blank", not "never set".
    public static bool HasApplied => File.Exists(AppliedMarker);

    public static void MarkApplied()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(AppliedMarker)!);
        File.WriteAllText(AppliedMarker, "");
    }

    public static string? Get(string name)
    {
        if (OperatingSystem.IsWindows())
            return Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User);

        // macOS: GUI apps don't inherit shell variables, so read the managed profile block.
        var block = ReadBlock(Path.Combine(Home, ".zshrc"));
        return block.TryGetValue(name, out var v) ? v : Environment.GetEnvironmentVariable(name);
    }

    public static void Set(IDictionary<string, string> values)
    {
        if (OperatingSystem.IsWindows())
        {
            foreach (var (k, v) in values)
            {
                Environment.SetEnvironmentVariable(k, string.IsNullOrEmpty(v) ? null : v, EnvironmentVariableTarget.User);
                Environment.SetEnvironmentVariable(k, string.IsNullOrEmpty(v) ? null : v, EnvironmentVariableTarget.Process);
            }
            return;
        }

        foreach (var profile in ShellProfiles)
            WriteBlock(Path.Combine(Home, profile), values);

        if (OperatingSystem.IsMacOS())
            foreach (var (k, v) in values)
                RunLaunchctl(string.IsNullOrEmpty(v) ? new[] { "unsetenv", k } : new[] { "setenv", k, v });
    }

    private static Dictionary<string, string> ReadBlock(string path)
    {
        var result = new Dictionary<string, string>();
        if (!File.Exists(path)) return result;
        var inBlock = false;
        foreach (var line in File.ReadLines(path))
        {
            if (line.Trim() == BeginMarker) { inBlock = true; continue; }
            if (line.Trim() == EndMarker) break;
            if (!inBlock) continue;
            var m = Regex.Match(line, @"^export (\w+)='(.*)'$");
            if (m.Success) result[m.Groups[1].Value] = m.Groups[2].Value.Replace("'\\''", "'");
        }
        return result;
    }

    private static void WriteBlock(string path, IDictionary<string, string> values)
    {
        var lines = File.Exists(path) ? File.ReadAllLines(path).ToList() : new List<string>();
        var start = lines.FindIndex(l => l.Trim() == BeginMarker);
        var end = lines.FindIndex(l => l.Trim() == EndMarker);
        if (start >= 0 && end > start) lines.RemoveRange(start, end - start + 1);

        var block = new List<string> { BeginMarker };
        foreach (var (k, v) in values)
            if (!string.IsNullOrEmpty(v))
                block.Add($"export {k}='{v.Replace("'", "'\\''")}'");
        block.Add(EndMarker);

        if (start >= 0 && end > start) lines.InsertRange(start, block);
        else lines.AddRange(block);

        File.WriteAllText(path, string.Join("\n", lines) + "\n", new UTF8Encoding(false));
    }

    private static void RunLaunchctl(string[] args)
    {
        var psi = new ProcessStartInfo("/bin/launchctl") { UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi);
        p?.WaitForExit();
    }
}
