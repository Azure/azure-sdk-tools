// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ApiExtractor.DotNet;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: ApiExtractor.DotNet <path> [--output file] [--pretty] [--csharp file]");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Options:");
    Console.Error.WriteLine("  -o, --output <file>   Write JSON API index to file");
    Console.Error.WriteLine("  -p, --pretty          Pretty-print JSON output");
    Console.Error.WriteLine("  -c, --csharp <file>   Write human-readable C# syntax to file");
    return 1;
}

var path = args[0];
string? output = null;
string? csharpOutput = null;
var pretty = false;

for (var i = 1; i < args.Length; i++)
{
    if (args[i] is "-o" or "--output" && i + 1 < args.Length)
    {
        output = args[++i];
    }
    else if (args[i] is "-p" or "--pretty")
    {
        pretty = true;
    }
    else if (args[i] is "-c" or "--csharp" && i + 1 < args.Length)
    {
        csharpOutput = args[++i];
    }
}

path = Path.GetFullPath(path);
if (!Directory.Exists(path))
{
    Console.Error.WriteLine($"Directory not found: {path}");
    return 1;
}

var extractor = new CSharpApiExtractor();
var index = await extractor.ExtractAsync(path, CancellationToken.None);

// Write JSON output
var json = pretty
    ? System.Text.Json.JsonSerializer.Serialize(index, new System.Text.Json.JsonSerializerOptions { WriteIndented = true })
    : index.ToJson();

if (output != null)
{
    await File.WriteAllTextAsync(output, json);
    Console.Error.WriteLine($"Wrote {json.Length:N0} chars to {output}");
}
else if (csharpOutput == null)
{
    // Only output JSON to stdout if no --csharp specified
    Console.WriteLine(json);
}

// Write C# syntax output
if (csharpOutput != null)
{
    var csharp = CSharpFormatter.Format(index);
    await File.WriteAllTextAsync(csharpOutput, csharp);
    Console.Error.WriteLine($"Wrote {csharp.Length:N0} chars to {csharpOutput}");
}

return 0;
