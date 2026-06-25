// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Models.AzureDevOps
{
    /// <summary>
    /// Contains product information extracted from a product work item
    /// </summary>
    public class ProductInfo
    {
        /// <summary>
        /// Product Service Tree ID
        /// </summary>
        public string ProductServiceTreeId { get; set; } = string.Empty;

        /// <summary>
        /// Associated Service Service Tree ID
        /// </summary>
        public string ServiceId { get; set; } = string.Empty;

        /// <summary>
        /// Package display name
        /// </summary>
        public string PackageDisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Product Service Tree Link
        /// </summary>
        public string ProductServiceTreeLink { get; set; } = string.Empty;

        /// <summary>
        /// Work item ID of the product/epic work item
        /// </summary>
        public int WorkItemId { get; set; }

        /// <summary>
        /// Title of the product work item
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Product name
        /// </summary>
        public string ProductName { get; set; } = string.Empty;

        /// <summary>
        /// Product type (e.g. Offering, Feature, SKU)
        /// </summary>
        public string ProductType { get; set; } = string.Empty;

        /// <summary>
        /// Product lifecycle
        /// </summary>
        public string ProductLifecycle { get; set; } = string.Empty;
    }
}
