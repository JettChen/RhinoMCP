using System.Runtime.InteropServices;

namespace RhMcp.Router;

// Resolves a full path to Rhino.exe (Windows) or the Rhinoceros binary (macOS)
// for a given version string. Versions: "8" | "9" | "WIP".
public class RhinoLocator
{
    public string ResolveRhinoExe(string version)
    {
        if (TryResolve(version, out string path))
            return path;

        throw new FileNotFoundException(
            $"Could not locate Rhino executable for version '{version}'. " +
            $"Installed versions found: {string.Join(", ", ListInstalledVersions())}");
    }

    private bool TryResolve(string version, out string path)
    {
        path = string.Empty;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // TODO: also try registry-based lookup if Program Files paths miss.
            string dir = version switch
            {
                "8" => @"C:\Program Files\Rhino 8",
                "9" => @"C:\Program Files\Rhino 9",
                "WIP" => @"C:\Program Files\Rhino 9 WIP",
                _ => string.Empty
            };
            if (dir.Length == 0) return false;
            path = Path.Combine(dir, "System", "Rhino.exe");
            return File.Exists(path);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            string appName = version switch
            {
                "8" => "Rhino 8.app",
                "9" => "Rhino 9.app",
                "WIP" => "RhinoWIP.app",
                _ => string.Empty
            };
            // Without this guard an unknown version resolves to "/Applications/",
            // which Directory.Exists trivially confirms — spawn then attempts
            // `open -a /Applications/` and we burn the full 60s startup timeout
            // before reporting failure. Fail fast with rhino_not_installed instead.
            if (appName.Length == 0) return false;
            path = $"/Applications/{appName}";
            return Directory.Exists(path);
        }

        return false;
    }

    public IEnumerable<string> ListInstalledVersions()
    {
        foreach (string v in new[] { "8", "9", "WIP" })
        {
            if (TryResolve(v, out _))
                yield return v;
        }
    }
}
