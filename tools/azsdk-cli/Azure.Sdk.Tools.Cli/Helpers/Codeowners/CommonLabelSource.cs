// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Helpers.Codeowners;

/// <summary>
/// The set of service labels a fragment is allowed to name, read from
/// <c>tools/github/data/common-labels.csv</c> in azure-sdk-tools.
/// <para>
/// This is the sanctioned common label set rather than the labels that happen to exist on one
/// repository. Fragments describe services, services span language repos, and a label that exists in
/// only one of them routes issues nowhere in the rest. Holding fragments to the common set is what
/// keeps a service's triage identical across repositories.
/// </para>
/// </summary>
public interface ICommonLabelSource
{
    /// <summary>Label names, compared case-insensitively.</summary>
    Task<IReadOnlySet<string>> GetLabelsAsync(CancellationToken ct);
}

public class CommonLabelSource(HttpClient httpClient, ILogger<CommonLabelSource> logger) : ICommonLabelSource
{
    public const string CommonLabelsCsvUrl =
        "https://raw.githubusercontent.com/Azure/azure-sdk-tools/refs/heads/main/tools/github/data/common-labels.csv";

    private IReadOnlySet<string>? cached;

    public async Task<IReadOnlySet<string>> GetLabelsAsync(CancellationToken ct)
    {
        if (cached != null)
        {
            return cached;
        }

        string csv;
        try
        {
            csv = await httpClient.GetStringAsync(CommonLabelsCsvUrl, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Fail closed. Treating an unreachable list as "no labels are known" would report every
            // label in the repository as invalid.
            throw new InvalidOperationException(
                $"Could not read the common label list from {CommonLabelsCsvUrl}: {ex.Message}", ex);
        }

        var labels = ParseLabelNames(csv);
        if (labels.Count == 0)
        {
            throw new InvalidOperationException(
                $"The common label list at {CommonLabelsCsvUrl} parsed to zero labels. " +
                "Refusing to validate labels against an empty list.");
        }

        logger.LogDebug("Read {Count} common labels.", labels.Count);
        cached = labels;

        return cached;
    }

    /// <summary>
    /// Takes the first field of each row. The file is <c>name,description,color</c>, and a label
    /// name containing a comma would be quoted, so honor quoting rather than splitting blindly.
    /// </summary>
    public static IReadOnlySet<string> ParseLabelNames(string csv)
    {
        var labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in csv.Split('\n'))
        {
            var row = line.Trim('\r', ' ');
            if (row.Length == 0)
            {
                continue;
            }

            var name = row.StartsWith('"')
                ? row[1..(row.IndexOf('"', 1) is var end && end > 0 ? end : row.Length)]
                : row.Split(',')[0];

            name = name.Trim();
            if (name.Length > 0)
            {
                labels.Add(name);
            }
        }

        return labels;
    }
}
