// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Models;

public enum ProductLifecycle
{
    Unknown,
    InDev,
    PrivatePreview,
    PublicPreview,
    GA
}

public static class ProductLifecycleExtensions
{
    private static IReadOnlyDictionary<string, ProductLifecycle> userInputToProductLifecycle
        = new List<(string Key, ProductLifecycle Value)>{
            ( "In Dev", ProductLifecycle.InDev ),
            ( "Private Preview", ProductLifecycle.PrivatePreview ),
            ( "Public Preview", ProductLifecycle.PublicPreview ),
            ( "GA", ProductLifecycle.GA ),
        }.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyDictionary<string, ProductLifecycle> adoFieldValueToProductLifecycle
        = new List<(string Key, ProductLifecycle Value)>{
            ( "In Dev", ProductLifecycle.InDev ),
            ( "Private Preview", ProductLifecycle.PrivatePreview ),
            ( "Public Preview", ProductLifecycle.PublicPreview ),
            ( "GA", ProductLifecycle.GA ),
        }.ToDictionary(kv => kv.Key, kv => kv.Value);

    private static IReadOnlyDictionary<ProductLifecycle, string> productLifecycleToAdoFieldValue
        = new List<(ProductLifecycle Key, string Value)>{
            ( ProductLifecycle.InDev, "In Dev" ),
            ( ProductLifecycle.PrivatePreview , "Private Preview" ),
            ( ProductLifecycle.PublicPreview , "Public Preview" ),
            ( ProductLifecycle.GA , "GA" ),
        }.ToDictionary(kv => kv.Key, kv => kv.Value);

    /// <summary>
    /// Converts a user-supplied string to an ProductLifecycle enum value (case-insensitive).
    /// </summary>
    public static bool TryParseFromUserInput(string? input, out ProductLifecycle result)
     => userInputToProductLifecycle.TryGetValue(input?.Trim(), out result);

    /// <summary>
    /// Converts an ADO work item field value to an ProductLifecycle enum value.
    /// </summary>
    public static ProductLifecycle FromAdoFieldValue(string? adoValue)
        => adoFieldValueToProductLifecycle.TryGetValue(adoValue, out var result) ? result : ProductLifecycle.Unknown;

    /// <summary>
    /// Returns the ADO work item field value for this product lifecycle.
    /// </summary>
    public static string ToAdoFieldValue(this ProductLifecycle productLifecycle)
        => productLifecycleToAdoFieldValue.TryGetValue(productLifecycle, out var result) ? result : string.Empty;

    /// <summary>
    /// Gets the list of valid user input values for product lifecycle.
    /// </summary>
    public static IEnumerable<string> ListValidUserInputs() => userInputToProductLifecycle.Keys;
}
