// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services.ClientUpdate;

/// <summary>
/// Loads the APIView <c>methodIndex</c> from one or more JSON files emitted by the Java APIView processor.
/// The processor currently produces one JSON file per input root.
/// </summary>
public static class JavaApiViewMethodIndexLoader
{
    public sealed record MethodIndexEntry(string[] ParamNames, string[] ParamTypes);

    /// <summary>
    /// Load and merge all method index entries from JSON files under the specified directory.
    /// </summary>
    /// <param name="dir">Directory containing APIView JSON outputs.</param>
    /// <param name="logger">Logger for debug diagnostics.</param>
    /// <returns>Dictionary mapping signature key to entry.</returns>
    public static Dictionary<string, MethodIndexEntry> LoadMerged(string dir, ILogger? logger = null)
    {
        var dict = new Dictionary<string, MethodIndexEntry>();
        if (!Directory.Exists(dir))
        {
            return dict;
        }
        foreach (var file in Directory.GetFiles(dir, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                using var stream = File.OpenRead(file);
                var root = JsonNode.Parse(stream);
                var methodIndexNode = root?["methodIndex"] as JsonObject;
                if (methodIndexNode == null)
                {
                    continue;
                }
                foreach (var kvp in methodIndexNode)
                {
                    if (kvp.Value is JsonObject entryObj)
                    {
                        var paramNames = entryObj["paramNames"] as JsonArray;
                        var paramTypes = entryObj["paramTypes"] as JsonArray;
                        string[] names = paramNames?.Select(n => n?.GetValue<string>() ?? string.Empty).ToArray() ?? Array.Empty<string>();
                        string[] types = paramTypes?.Select(t => t?.GetValue<string>() ?? string.Empty).ToArray() ?? Array.Empty<string>();
                        dict[kvp.Key] = new MethodIndexEntry(names, types);
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.LogDebug(ex, "Failed to parse methodIndex from {File}", file);
            }
        }
        return dict;
    }

    /// <summary>
    /// Compute method-level API changes between old and new indices.
    /// Produces MethodAdded, MethodRemoved, and MethodParameterNameChanged events.
    /// </summary>
    public static List<ApiChange> ComputeChanges(
        IReadOnlyDictionary<string, MethodIndexEntry> oldIndex,
        IReadOnlyDictionary<string, MethodIndexEntry> newIndex)
    {
        var changes = new List<ApiChange>();

        // Added methods
        foreach (var added in newIndex.Keys.Except(oldIndex.Keys))
        {
            changes.Add(new ApiChange
            {
                Kind = "MethodAdded",
                Symbol = added,
                Detail = $"Method added: {added}",
                Metadata = new Dictionary<string, string>()
            });
        }

        // Removed methods
        foreach (var removed in oldIndex.Keys.Except(newIndex.Keys))
        {
            changes.Add(new ApiChange
            {
                Kind = "MethodRemoved",
                Symbol = removed,
                Detail = $"Method removed: {removed}",
                Metadata = new Dictionary<string, string>()
            });
        }

        // Parameter name changes
        foreach (var common in oldIndex.Keys.Intersect(newIndex.Keys))
        {
            var o = oldIndex[common];
            var n = newIndex[common];
            if (o.ParamNames.Length == n.ParamNames.Length && o.ParamNames.Length > 0)
            {
                for (int i = 0; i < o.ParamNames.Length; i++)
                {
                    if (!string.Equals(o.ParamNames[i], n.ParamNames[i], StringComparison.Ordinal))
                    {
                        changes.Add(new ApiChange
                        {
                            Kind = "MethodParameterNameChanged",
                            Symbol = common,
                            Detail = $"Parameter name changed at position {i}: '{o.ParamNames[i]}' -> '{n.ParamNames[i]}'",
                            Metadata = new Dictionary<string, string>
                            {
                                ["parameterIndex"] = i.ToString(),
                                ["oldName"] = o.ParamNames[i],
                                ["newName"] = n.ParamNames[i]
                            }
                        });
                    }
                }
            }
        }

        return changes;
    }
}
