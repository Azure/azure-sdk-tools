// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ApiExtractor.Python;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: ApiExtractor.Python <path> [--output file] [--python file] [--pretty]");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Options:");
    Console.Error.WriteLine("  -o, --output <file>   Write JSON API index to file");
    Console.Error.WriteLine("  -p, --pretty          Pretty-print JSON output");
    Console.Error.WriteLine("  -y, --python <file>   Write human-readable Python stubs to file");
    return 1;
}

var path = args[0];
string? output = null;
string? pythonOutput = null;
var pretty = false;

for (var i = 1; i < args.Length; i++)
{
    if (args[i] is "-o" or "--output" && i + 1 < args.Length)
        output = args[++i];
    else if (args[i] is "-p" or "--pretty")
        pretty = true;
    else if (args[i] is "-y" or "--python" && i + 1 < args.Length)
        pythonOutput = args[++i];
}

path = Path.GetFullPath(path);
if (!Directory.Exists(path))
{
    Console.Error.WriteLine($"Directory not found: {path}");
    return 1;
}

var extractor = new PythonApiExtractor();
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
else if (pythonOutput == null)
{
    Console.WriteLine(json);
}

// Write Python stub output
if (pythonOutput != null)
{
    var stub = PythonFormatter.Format(index);
    await File.WriteAllTextAsync(pythonOutput, stub);
    Console.Error.WriteLine($"Wrote {stub.Length:N0} chars to {pythonOutput}");
}

return 0;
