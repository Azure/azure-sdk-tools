// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Models;

public enum ProductType
{
    Unknown,
    Offering,
    Feature,
    Sku
}

public static class ProductTypeExtensions
{
    private const string AdoOffering = "Offering";
    private const string AdoFeature = "Feature";
    private const string AdoSku = "SKU";

    /// <summary>
    /// Converts a user-supplied string to a ProductType enum value (case-insensitive).
    /// </summary>
    public static bool TryParseFromUserInput(string? input, out ProductType result)
    {
        result = ProductType.Unknown;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var trimmed = input.Trim();
        if (trimmed.Equals("Offering", StringComparison.OrdinalIgnoreCase))
        {
            result = ProductType.Offering;
        }
        else if (trimmed.Equals("Feature", StringComparison.OrdinalIgnoreCase))
        {
            result = ProductType.Feature;
        }
        else if (trimmed.Equals("Sku", StringComparison.OrdinalIgnoreCase))
        {
            result = ProductType.Sku;
        }
        return result != ProductType.Unknown;
    }

    /// <summary>
    /// Converts an ADO work item field value to a ProductType enum value.
    /// </summary>
    public static ProductType FromAdoFieldValue(string? adoValue)
    {
        if (string.IsNullOrWhiteSpace(adoValue))
        {
            return ProductType.Unknown;
        }

        if (adoValue.Equals(AdoOffering, StringComparison.OrdinalIgnoreCase))
        {
            return ProductType.Offering;
        }

        if (adoValue.Equals(AdoFeature, StringComparison.OrdinalIgnoreCase))
        {
            return ProductType.Feature;
        }

        if (adoValue.Equals(AdoSku, StringComparison.OrdinalIgnoreCase))
        {
            return ProductType.Sku;
        }

        return ProductType.Unknown;
    }

    /// <summary>
    /// Returns the ADO work item field value for this product type.
    /// </summary>
    public static string ToAdoFieldValue(this ProductType productType) => productType switch
    {
        ProductType.Offering => AdoOffering,
        ProductType.Feature => AdoFeature,
        ProductType.Sku => AdoSku,
        _ => string.Empty
    };
}
