// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Models;

public enum DataPlaneApplicability
{
    Unknown,
    NotApplicable,
    No,
    Yes,
}

public static class DataPlaneApplicabilityExtensions
{
    private static IReadOnlyDictionary<string, DataPlaneApplicability> userInputToDataPlaneApplicability
        = new List<(string Key, DataPlaneApplicability Value)>{
            ( "N/A", DataPlaneApplicability.NotApplicable ),
            ( "No", DataPlaneApplicability.No ),
            ( "Yes", DataPlaneApplicability.Yes ),
        }.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyDictionary<string, DataPlaneApplicability> adoFieldValueToDataPlaneApplicability
        = new List<(string Key, DataPlaneApplicability Value)>{
            ( "unsure", DataPlaneApplicability.NotApplicable ),
            ( "No", DataPlaneApplicability.No ),
            ( "Yes", DataPlaneApplicability.Yes ),
        }.ToDictionary(kv => kv.Key, kv => kv.Value);

    private static IReadOnlyDictionary<DataPlaneApplicability, string> dataPlaneApplicabilityToAdoFieldValue
        = new List<(DataPlaneApplicability Key, string Value)>{
            ( DataPlaneApplicability.NotApplicable, "unsure" ),
            ( DataPlaneApplicability.No, "No" ),
            ( DataPlaneApplicability.Yes , "Yes" ),
        }.ToDictionary(kv => kv.Key, kv => kv.Value);

    /// <summary>
    /// Converts a user-supplied string to an DataPlaneApplicability enum value (case-insensitive).
    /// </summary>
    public static bool TryParseFromUserInput(string? input, out DataPlaneApplicability result)
        => userInputToDataPlaneApplicability.TryGetValue(input?.Trim(), out result);

    /// <summary>
    /// Converts an ADO work item field value to an DataPlaneApplicability enum value.
    /// </summary>
    public static DataPlaneApplicability FromAdoFieldValue(string? adoValue)
        => adoFieldValueToDataPlaneApplicability.TryGetValue(adoValue, out var result) ? result : DataPlaneApplicability.Unknown;

    /// <summary>
    /// Returns the ADO work item field value for this data plane applicability.
    /// </summary>
    public static string ToAdoFieldValue(this DataPlaneApplicability dataPlaneApplicability)
        => dataPlaneApplicabilityToAdoFieldValue.TryGetValue(dataPlaneApplicability, out var result) ? result : string.Empty;

    /// <summary>
    /// Gets the list of valid user input values for data plane applicability.
    /// </summary>
    public static IEnumerable<string> ListValidUserInputs() => userInputToDataPlaneApplicability.Keys;
}
