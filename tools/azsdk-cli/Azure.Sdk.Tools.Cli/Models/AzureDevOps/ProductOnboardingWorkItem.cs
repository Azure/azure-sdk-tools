// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Attributes;
using Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlan;

namespace Azure.Sdk.Tools.Cli.Models.AzureDevOps
{
    public class ProductOnboardingWorkItem : WorkItemBase
    {
        [FieldName("Custom.ProductServiceTreeID")]
        public string ProductId { get; set; } = string.Empty;

        [FieldName("Custom.ProductName")]
        public string ProductName { get; set; } = string.Empty;

        [FieldName("Custom.ProductType")]
        public string ProductType { get; set; } = string.Empty;

        [FieldName("Custom.ProductLifecycle")]
        public string ProductLifecycle { get; set; } = string.Empty;

        [FieldName("Custom.AssociatedServiceServiceTreeID")]
        public string ServiceId { get; set; } = string.Empty;

        [FieldName("Custom.dc40c65c-202b-4c93-8958-8e4b92c75e54")]
        public string ServiceName { get; set; } = string.Empty;

        [FieldName("Custom.DataScope")]
        public string DataPlane { get; set; } = string.Empty;

        [FieldName("Custom.MgmtScope")]
        public string ManagementPlane { get; set; } = string.Empty;

        [FieldName("Custom.OnboardingQuestionnaireSubmittedby")]
        public string Submitter { get; set; } = string.Empty;

        public bool IsTestProductOnboarding { get; set; } = true;

        public static string TestFieldName { get; } = "System.Tags";
        public static string TestFieldTestValue { get; } = "Release Planner App Test";

        public static string WorkItemTypeFieldName { get; } = "System.WorkItemType";
        public static string WorkItemTypeValue { get; } = "Triage";

        public ProductOnboardingStatus ToProductOnboardingStatus()
            => new ()
                {
                    ProductId = Guid.TryParse(ProductId, out var productId) ? productId : Guid.Empty,
                    ProductName = ProductName,
                    ProductType = ProductTypeExtensions.FromAdoFieldValue(ProductType),
                    ProductLifecycle = ProductLifecycleExtensions.FromAdoFieldValue(ProductLifecycle),
                    ServiceId = Guid.TryParse(ServiceId, out var serviceId) ? serviceId : Guid.Empty,
                    ServiceName = ServiceName,
                    DataPlane = DataPlaneApplicabilityExtensions.FromAdoFieldValue(DataPlane),
                    ManagementPlane = ManagementPlaneApplicabilityExtensions.FromAdoFieldValue(ManagementPlane),
                    Submitter = Submitter,
                };

        public ProductOnboardingResponse PopulateProductOnboardingResponse(ProductOnboardingResponse response)
        {
            response.ProductOnboardingDetails = this;
            var status = ToProductOnboardingStatus();
            
            response.ProductId = status.ProductId;
            response.ProductName = status.ProductName;
            response.ProductType = status.ProductType;
            response.ProductLifecycle = status.ProductLifecycle;
            response.ServiceId = status.ServiceId;
            response.ServiceName = status.ServiceName;
            response.DataPlane = status.DataPlane;
            response.ManagementPlane = status.ManagementPlane;
            response.Submitter = status.Submitter;

            response.NeedsSDK = (response.DataPlane == DataPlaneApplicability.Yes || response.ManagementPlane == ManagementPlaneApplicability.Yes);
            return response;
        }

        public void SetFromProductOnboardingStatus(ProductOnboardingStatus status)
        {
            ProductId = status.ProductId.ToString();
            ProductName = status.ProductName;
            ProductType = status.ProductType.ToAdoFieldValue();
            ProductLifecycle = status.ProductLifecycle.ToAdoFieldValue();
            ServiceId = status.ServiceId.ToString();
            ServiceName = status.ServiceName;
            DataPlane = status.DataPlane.ToAdoFieldValue();
            ManagementPlane = status.ManagementPlane.ToAdoFieldValue();
            Submitter = status.Submitter;
        }
    }

    public class ProductOnboardingStatus
    {
        public Guid ProductId { get; set; } = Guid.Empty;
        public string ProductName { get; set; } = string.Empty;
        public ProductType ProductType { get; set; } = ProductType.Unknown;
        public ProductLifecycle ProductLifecycle { get; set; } = ProductLifecycle.Unknown;
        public Guid ServiceId { get; set; } = Guid.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public DataPlaneApplicability DataPlane { get; set; } = DataPlaneApplicability.Unknown;
        public ManagementPlaneApplicability ManagementPlane { get; set; } = ManagementPlaneApplicability.Unknown;
        public string Submitter { get; set; } = string.Empty;
    }
}
