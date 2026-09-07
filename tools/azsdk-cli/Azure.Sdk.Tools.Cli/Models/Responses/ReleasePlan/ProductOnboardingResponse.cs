// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;

namespace Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlan
{
    /// <summary>
    /// Represents a response containing product onboarding work item details and the result of an onboarding operation.
    /// </summary>
    public class ProductOnboardingResponse : CommandResponse
    {
        [JsonPropertyName("product_id")]
        public Guid ProductId { get; set; } = Guid.Empty;

        [JsonPropertyName("product_name")]
        public string ProductName { get; set; } = string.Empty;

        [JsonPropertyName("product_type")]
        public ProductType ProductType { get; set; } = ProductType.Unknown;

        [JsonPropertyName("product_lifecycle")]
        public ProductLifecycle ProductLifecycle { get; set; } = ProductLifecycle.Unknown;

        [JsonPropertyName("service_id")]
        public Guid ServiceId { get; set; } = Guid.Empty;

        [JsonPropertyName("service_name")]
        public string ServiceName { get; set; } = string.Empty;

        [JsonPropertyName("needs_sdk")]
        public bool NeedsSDK { get; set; } = false;

        [JsonPropertyName("data_plane")]
        public DataPlaneApplicability DataPlane { get; set; } = DataPlaneApplicability.Unknown;

        [JsonPropertyName("management_plane")]
        public ManagementPlaneApplicability ManagementPlane { get; set; } = ManagementPlaneApplicability.Unknown;

        [JsonPropertyName("submitter")]
        public string Submitter { get; set; } = string.Empty;

        public ProductOnboardingWorkItem? ProductOnboardingDetails { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("warnings")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? Warnings { get; set; }
        
        protected override string Format()
        {
            var result = new StringBuilder();
            if (ProductOnboardingDetails != null)
            {
                result.AppendLine($"Work Item ID: {ProductOnboardingDetails.WorkItemId}");
                result.AppendLine($"Product ID: {ProductOnboardingDetails.ProductId}");
                result.AppendLine($"Product Name: {ProductOnboardingDetails.ProductName}");
                result.AppendLine($"Product Type: {ProductOnboardingDetails.ProductType}");
                result.AppendLine($"Product Lifecycle: {ProductOnboardingDetails.ProductLifecycle}");
                result.AppendLine($"Service ID: {ProductOnboardingDetails.ServiceId}");
                result.AppendLine($"Service Name: {ProductOnboardingDetails.ServiceName}");
                result.AppendLine($"Data Plane: {ProductOnboardingDetails.DataPlane}");
                result.AppendLine($"Management Plane: {ProductOnboardingDetails.ManagementPlane}");
                result.AppendLine($"Submitter: {ProductOnboardingDetails.Submitter}");
            }
            else
            {
                result.AppendLine("No product onboarding details available.");
            }
            if (Warnings?.Count > 0)
            {
                foreach (var warning in Warnings)
                {
                    result.AppendLine($"[WARNING] {warning}");
                }
            }
            return result.ToString();
        }
    }
}
