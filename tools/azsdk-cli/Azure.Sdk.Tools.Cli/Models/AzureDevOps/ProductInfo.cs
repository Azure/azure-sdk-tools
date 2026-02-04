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
        /// Service ID (same as Service Tree ID)
        /// </summary>
        public string ServiceId { get; set; } = string.Empty;

        /// <summary>
        /// Package display name
        /// </summary>
        public string PackageDisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Product contact PM (Primary PM)
        /// </summary>
        public string ProductContactPM { get; set; } = string.Empty;

        /// <summary>
        /// Work item ID of the product/epic work item
        /// </summary>
        public int WorkItemId { get; set; }

        /// <summary>
        /// Title of the product work item
        /// </summary>
        public string Title { get; set; } = string.Empty;
    }
}
