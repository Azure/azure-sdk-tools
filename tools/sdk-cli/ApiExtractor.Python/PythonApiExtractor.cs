// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json;

namespace ApiExtractor.Python;

/// <summary>
/// Extracts public API surface from Python source files.
/// Shells out to Python's ast module for proper parsing.
/// </summary>
public class PythonApiExtractor
{
    private static readonly string ScriptPath = Path.Combine(
        Path.GetDirectoryName(typeof(PythonApiExtractor).Assembly.Location) ?? ".",
        "extract_api.py");

    public async Task<ApiIndex> ExtractAsync(string rootPath, CancellationToken ct = default)
    {
        // Find python executable
        var python = FindPython();
        if (python == null)
            throw new InvalidOperationException("Python 3 not found. Install Python 3.9+ and ensure it's in PATH.");

        // Get script path - embedded in assembly directory
        var scriptPath = GetScriptPath();

        var psi = new ProcessStartInfo
        {
            FileName = python,
            Arguments = $"\"{scriptPath}\" \"{rootPath}\" --json",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start Python");

        var output = await process.StandardOutput.ReadToEndAsync(ct);
        var error = await process.StandardError.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Python extractor failed: {error}");

        // Parse JSON output
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var raw = JsonSerializer.Deserialize<RawApiIndex>(output, options)
            ?? throw new InvalidOperationException("Failed to parse Python extractor output");

        return ConvertToApiIndex(raw);
    }

    private static string? FindPython()
    {
        // Try common Python executables
        foreach (var name in new[] { "python3", "python" })
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = name,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                if (p != null)
                {
                    p.WaitForExit(1000);
                    if (p.ExitCode == 0)
                        return name;
                }
            }
            catch { }
        }
        return null;
    }

    private static string GetScriptPath()
    {
        // Check assembly directory first
        if (File.Exists(ScriptPath))
            return ScriptPath;

        // Check relative to current directory
        var local = Path.Combine(Directory.GetCurrentDirectory(), "extract_api.py");
        if (File.Exists(local))
            return local;

        // Try to find in source tree (for development)
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "tools", "sdk-cli", "ApiExtractor.Python", "extract_api.py");
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException("extract_api.py not found");
    }

    private static ApiIndex ConvertToApiIndex(RawApiIndex raw)
    {
        var modules = raw.Modules?.Select(m => new ModuleInfo(
            m.Name ?? "",
            m.Classes?.Select(c => new ClassInfo(
                c.Name ?? "",
                c.Base,
                c.Doc,
                c.Methods?.Select(mt => new MethodInfo(
                    mt.Name ?? "",
                    mt.Sig ?? "",
                    mt.Doc,
                    mt.Async,
                    mt.Classmethod,
                    mt.Staticmethod
                )).ToList(),
                c.Properties?.Select(p => new PropertyInfo(p.Name ?? "", p.Type, p.Doc)).ToList()
            )).ToList(),
            m.Functions?.Select(f => new FunctionInfo(
                f.Name ?? "",
                f.Sig ?? "",
                f.Doc,
                f.Async
            )).ToList()
        )).ToList() ?? [];

        return new ApiIndex(raw.Package ?? "", modules);
    }

    // Raw JSON models for deserialization
    private record RawApiIndex(string? Package, List<RawModule>? Modules);
    private record RawModule(string? Name, List<RawClass>? Classes, List<RawFunction>? Functions);
    private record RawClass(string? Name, string? Base, string? Doc, List<RawMethod>? Methods, List<RawProperty>? Properties);
    private record RawMethod(string? Name, string? Sig, string? Doc, bool? Async, bool? Classmethod, bool? Staticmethod);
    private record RawProperty(string? Name, string? Type, string? Doc);
    private record RawFunction(string? Name, string? Sig, string? Doc, bool? Async);
}
