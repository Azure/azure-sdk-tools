// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;

namespace Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlan
{
    /// <summary>
    /// Represents a response containing product information found from a TypeSpec project path
    /// </summary>
    public class ProductInfoResponse : ReleasePlanBaseResponse
    {
        [JsonPropertyName("product_info")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ProductInfo? ProductInfo { get; set; }

        [JsonPropertyName("message")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Message { get; set; }

        protected override string Format()
        {
            var result = new StringBuilder();
            if (ProductInfo != null)
            {
                result.AppendLine($"Product Service Tree ID: {ProductInfo.ProductServiceTreeId}");
                result.AppendLine($"Service ID: {ProductInfo.ServiceId}");
                result.AppendLine($"Package Display Name: {ProductInfo.PackageDisplayName}");
                result.AppendLine($"Product Contact PM: {ProductInfo.ProductContactPM}");
                result.AppendLine($"Product Work Item ID: {ProductInfo.WorkItemId}");
                result.AppendLine($"Product Title: {ProductInfo.Title}");
            }
            else
            {
                result.AppendLine("No product information available.");
            }

            if (!string.IsNullOrEmpty(Message))
            {
                result.AppendLine();
                result.AppendLine(Message);
            }

            return result.ToString();
        }
    }
}
