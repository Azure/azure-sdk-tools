// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Models;

public enum ManagementPlaneApplicability
{
    Unknown,
    NotApplicable,
    No,
    Yes,
}

public static class ManagementPlaneApplicabilityExtensions
{
    private static IReadOnlyDictionary<string, ManagementPlaneApplicability> userInputToManagementPlaneApplicability
        = new List<(string Key, ManagementPlaneApplicability Value)>{
            ( "N/A", ManagementPlaneApplicability.NotApplicable ),
            ( "No", ManagementPlaneApplicability.No ),
            ( "Yes", ManagementPlaneApplicability.Yes ),
        }.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyDictionary<string, ManagementPlaneApplicability> adoFieldValueToManagementPlaneApplicability
        = new List<(string Key, ManagementPlaneApplicability Value)>{
            ( "unsure", ManagementPlaneApplicability.NotApplicable ),
            ( "No", ManagementPlaneApplicability.No ),
            ( "Yes", ManagementPlaneApplicability.Yes ),
        }.ToDictionary(kv => kv.Key, kv => kv.Value);

    private static IReadOnlyDictionary<ManagementPlaneApplicability, string> managementPlaneApplicabilityToAdoFieldValue
        = new List<(ManagementPlaneApplicability Key, string Value)>{
            ( ManagementPlaneApplicability.NotApplicable, "unsure" ),
            ( ManagementPlaneApplicability.No, "No" ),
            ( ManagementPlaneApplicability.Yes , "Yes" ),
        }.ToDictionary(kv => kv.Key, kv => kv.Value);

    /// <summary>
    /// Converts a user-supplied string to an ManagementPlaneApplicability enum value (case-insensitive).
    /// </summary>
    public static bool TryParseFromUserInput(string? input, out ManagementPlaneApplicability result)
        => userInputToManagementPlaneApplicability.TryGetValue(input?.Trim(), out result);

    /// <summary>
    /// Converts an ADO work item field value to an ManagementPlaneApplicability enum value.
    /// </summary>
    public static ManagementPlaneApplicability FromAdoFieldValue(string? adoValue)
        => adoFieldValueToManagementPlaneApplicability.TryGetValue(adoValue, out var result) ? result : ManagementPlaneApplicability.Unknown;

    /// <summary>
    /// Returns the ADO work item field value for this management plane applicability.
    /// </summary>
    public static string ToAdoFieldValue(this ManagementPlaneApplicability managementPlaneApplicability)
        => managementPlaneApplicabilityToAdoFieldValue.TryGetValue(managementPlaneApplicability, out var result) ? result : string.Empty;

    /// <summary>
    /// Gets the list of valid user input values for management plane applicability.
    /// </summary>
    public static IEnumerable<string> ListValidUserInputs() => userInputToManagementPlaneApplicability.Keys;
}
