// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Helpers;

public interface IEnvFileHelper
{
    /// <summary>
    /// Parses a .env file and returns a dictionary of key-value pairs.
    /// </summary>
    /// <param name="path">Path to the .env file.</param>
    /// <returns>A dictionary containing the parsed environment variables.</returns>
    IDictionary<string, string> ParseEnvFile(string path);
}

public class EnvFileHelper : IEnvFileHelper
{
    public IDictionary<string, string> ParseEnvFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Environment file not found: {path}", path);
        }

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();

            // Skip empty lines and comments
            if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();

            // Remove surrounding quotes (single or double)
            if (value.Length >= 2 &&
                ((value.StartsWith('"') && value.EndsWith('"')) ||
                 (value.StartsWith('\'') && value.EndsWith('\''))))
            {
                value = value[1..^1];
            }

            result[key] = value;
        }

        return result;
    }
}
