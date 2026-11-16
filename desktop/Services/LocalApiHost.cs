using System.Diagnostics;
using WasteVision.Desktop.Services.Interfaces;

namespace WasteVision.Desktop.Services;

public sealed class LocalApiHost : ILocalApiHost
{
    private Process? _process;

    public async Task<bool> EnsureRunningAsync(IWasteApiClient api, CancellationToken cancellationToken = default)
    {
        if (await api.IsAvailableAsync(cancellationToken)) return true;
        var projectRoot = FindProjectRoot();
        if (projectRoot is null) return false;
        var python = FindPython(projectRoot);
        if (python is null) return false;

        var start = new ProcessStartInfo(python, "-m uvicorn app:app --host 127.0.0.1 --port 8765")
        {
            WorkingDirectory = projectRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        ApplyPythonPath(projectRoot, start.Environment);
        start.Environment.Remove("WASTE_WEB_USERNAME");
        start.Environment.Remove("WASTE_WEB_PASSWORD");

        try { _process = Process.Start(start); }
        catch { return false; }

        for (var attempt = 0; attempt < 40; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await api.IsAvailableAsync(cancellationToken)) return true;
            if (_process is null || _process.HasExited) break;
            await Task.Delay(500, cancellationToken);
        }
        return await api.IsAvailableAsync(cancellationToken);
    }

    private static void ApplyPythonPath(string root, IDictionary<string, string?> environment)
    {
        var dirs = new List<string>();
        var p1 = Path.Combine(root, ".runtime-fastapi");
        var p2 = Path.Combine(root, ".runtime-packages");
        if (Directory.Exists(p1)) dirs.Add(p1);
        if (Directory.Exists(p2)) dirs.Add(p2);
        if (dirs.Count > 0)
        {
            var existing = environment.TryGetValue("PYTHONPATH", out var value) ? value : null;
            if (!string.IsNullOrWhiteSpace(existing)) dirs.Add(existing);
            environment["PYTHONPATH"] = string.Join(Path.PathSeparator.ToString(), dirs);
        }
    }

    private static string? FindProjectRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "app.py")) && File.Exists(Path.Combine(current.FullName, "waste_sorting_helpers.py")))
                return current.FullName;
            current = current.Parent;
        }
        return null;
    }

    private static string? FindPython(string root)
    {
        var candidates = new List<string>();
        var configured = Environment.GetEnvironmentVariable("WASTE_PYTHON");
        if (!string.IsNullOrWhiteSpace(configured)) candidates.Add(configured);
        candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "anaconda3", "python.exe"));
        candidates.Add(Path.Combine(root, ".venv", "Scripts", "python.exe"));
        candidates.Add(@"C:\Users\ibral\anaconda3\python.exe");
        candidates.Add("python.exe");

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var start = new ProcessStartInfo(candidate, "-c \"import fastapi, uvicorn, numpy, PIL, sklearn\"")
                {
                    WorkingDirectory = root,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                ApplyPythonPath(root, start.Environment);
                var probe = Process.Start(start);
                probe?.WaitForExit(12000);
                if (probe is { HasExited: true, ExitCode: 0 }) return candidate;
                if (probe is { HasExited: false }) probe.Kill(true);
                probe?.Dispose();
            }
            catch { }
        }
        return null;
    }

    public void Dispose()
    {
        try
        {
            if (_process is { HasExited: false })
            {
                _process.Kill(true);
                _process.Dispose();
            }
        }
        catch { }
    }
}
