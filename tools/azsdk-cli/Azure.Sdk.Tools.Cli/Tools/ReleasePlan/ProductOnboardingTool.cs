// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlan;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tools.Core;
using Microsoft.TeamFoundation.Common;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.ReleasePlan
{
    [Description("Product Onboarding Tool type that contains tools to connect to Azure DevOps to work with product onboarding work items")]
    [McpServerToolType]
    public partial class ProductOnboardingTool(  // partial class required due to source generated regex
        IDevOpsService devOpsService,
        ILogger<ProductOnboardingTool> logger,
        IEnvironmentHelper environmentHelper
    ) : MCPMultiCommandTool
    {
        public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.ReleasePlan];

        // Commands
        private const string OnboardProductCommandName = "onboard-product";

        // MCP Tool Names
        private const string OnboardProductToolName = "azsdk_onboard_product";

        // Options
        private readonly Option<Guid> productIdOpt = new("--product-id")
        {
            Description = "Product ID",
            Required = true,
        };

        private readonly Option<string> productNameOpt = new("--product-name")
        {
            Description = "Product Name",
            Required = true,
        };

        private readonly Option<string> productTypeOpt = new("--product-type")
        {
            Description = "Product Type",
            Required = true,
        };

        private readonly Option<string> productLifecycleOpt = new("--product-lifecycle")
        {
            Description = "Product Lifecycle",
            Required = true,
        };

        private readonly Option<Guid> serviceIdOpt = new("--service-id")
        {
            Description = "Service ID",
            Required = true,
        };

        private readonly Option<string> serviceNameOpt = new("--service-name")
        {
            Description = "Service Name",
            Required = true,
        };

        private readonly Option<bool> needsSdkOpt = new("--needs-sdk")
        {
            Description = "Indicates whether an SDK is needed",
            Required = true,
        };

        private readonly Option<string> dataPlaneOpt = new("--data-plane")
        {
            Description = "Indicates whether a data plane SDK is applicable",
            Required = false,
            DefaultValueFactory = _ => "N/A",
        };

        private readonly Option<string> managementPlaneOpt = new("--management-plane")
        {
            Description = "Indicates whether a management plane SDK is applicable",
            Required = false,
            DefaultValueFactory = _ => "N/A",
        };

        private readonly Option<string> submitterOpt = new("--submitter")
        {
            Description = "Submitter handle",
            Required = true,
        };

        protected override List<Command> GetCommands() =>
        [
            new McpCommand(OnboardProductCommandName, "Sync product onboarding status", OnboardProductToolName)
            {
                productIdOpt,
                productNameOpt,
                productTypeOpt,
                productLifecycleOpt,
                serviceIdOpt,
                serviceNameOpt,
                needsSdkOpt,
                dataPlaneOpt,
                managementPlaneOpt,
                submitterOpt,
            },
        ];

        public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
        {
            var commandParser = parseResult;
            var command = commandParser.CommandResult.Command.Name;
            switch (command)
            {
                case OnboardProductCommandName:
                    var productId = commandParser.GetValue(productIdOpt);
                    var productName = commandParser.GetValue(productNameOpt);
                    var productType = commandParser.GetValue(productTypeOpt);
                    var productLifecycle = commandParser.GetValue(productLifecycleOpt);
                    var serviceId = commandParser.GetValue(serviceIdOpt);
                    var serviceName = commandParser.GetValue(serviceNameOpt);
                    var needsSdk = commandParser.GetValue(needsSdkOpt);
                    var dataPlane = commandParser.GetValue(dataPlaneOpt);
                    var managementPlane = commandParser.GetValue(managementPlaneOpt);
                    var submitter = commandParser.GetValue(submitterOpt);
                    return await OnboardProduct(
                        productId,
                        productName,
                        productType,
                        productLifecycle,
                        serviceId,
                        serviceName,
                        needsSdk,
                        dataPlane,
                        managementPlane,
                        submitter,
                        ct);

                default:
                    logger.LogError("Unknown command: {command}", command);
                    return new DefaultCommandResponse { ResponseError = $"Unknown command: '{command}'" };
            }
        }

        public static string TestEnvVarName { get; } = "AZSDKTOOLS_AGENT_TESTING";

        [McpServerTool(Name = OnboardProductToolName), Description("Create or update a product onboarding work item.")]
        public async Task<ProductOnboardingResponse> OnboardProduct(
            Guid productId,
            string productName,
            string productType,
            string productLifecycle,
            Guid serviceId,
            string serviceName,
            bool needsSdk,
            string dataPlane,
            string managementPlane,
            string submitter,
            CancellationToken ct)
        {
            try
            {
                if (productName.Trim().IsNullOrEmpty())
                {
                    return new () { ResponseError = "Empty product name." };
                }

                if (serviceName.Trim().IsNullOrEmpty())
                {
                    return new () { ResponseError = "Empty service name." };
                }

                if (submitter.Trim().IsNullOrEmpty())
                {
                    return new () { ResponseError = "Empty submitter." };
                }

                var pt = ProductType.Unknown;
                if (!ProductTypeExtensions.TryParseFromUserInput(productType, out pt) || pt == ProductType.Unknown)
                {
                    return new () {
                        ResponseError
                            = $"Invalid product type '{productType}'. Allowed values: {
                                string.Join(", ",  ProductTypeExtensions.ListValidUserInputs().Select(t => $"'{t}'"))}." };
                }

                var pl = ProductLifecycle.Unknown;
                if (!ProductLifecycleExtensions.TryParseFromUserInput(productLifecycle, out pl) || pl == ProductLifecycle.Unknown)
                {
                    return new ()
                    {
                        ResponseError
                            = $"Invalid product lifecycle '{productLifecycle}'. Allowed values: {
                                string.Join(", ", ProductLifecycleExtensions.ListValidUserInputs().Select(t => $"'{t}'"))}."
                    };
                }

                var dp = DataPlaneApplicability.Unknown;
                var mp = ManagementPlaneApplicability.Unknown;
                if (needsSdk)
                {
                    if (!DataPlaneApplicabilityExtensions.TryParseFromUserInput(dataPlane, out dp) || dp == DataPlaneApplicability.Unknown)
                    {
                        return new ()
                        {
                            ResponseError
                            = $"Invalid data plane applicability '{dataPlane}'. Allowed values: {
                                string.Join(", ", DataPlaneApplicabilityExtensions.ListValidUserInputs().Select(t => $"'{t}'"))}."
                        };
                    }

                    if (!ManagementPlaneApplicabilityExtensions.TryParseFromUserInput(managementPlane, out mp) || mp == ManagementPlaneApplicability.Unknown)
                    {
                        return new ()
                        {
                            ResponseError
                            = $"Invalid management plane applicability '{managementPlane}'. Allowed values: {
                                string.Join(", ", ManagementPlaneApplicabilityExtensions.ListValidUserInputs().Select(t => $"'{t}'"))}."
                        };
                    }
                }

                var status = new ProductOnboardingStatus
                {
                    ProductId = productId,
                    ProductName = productName,
                    ProductType = pt,
                    ProductLifecycle = pl,
                    ServiceId = serviceId,
                    ServiceName = serviceName,
                    DataPlane = dp,
                    ManagementPlane = mp,
                    Submitter = submitter,
                };

                var isTest = environmentHelper.GetBooleanVariable(TestEnvVarName, false);
                if (isTest)
                {
                    logger.LogInformation("'{TestEnvVarName}' environment variable is set to true, onboarding test product", TestEnvVarName);
                }

                ProductOnboardingWorkItem? productOnboarding = await devOpsService.GetProductOnboardingAsync(status.ProductId, status.ServiceId, ct, isTest);
                if (productOnboarding == null)
                {
                    productOnboarding = await devOpsService.CreateProductOnboardingAsync(status, ct, isTest);
                }
                else
                {
                    productOnboarding = await devOpsService.UpdateProductOnboardingAsync(productOnboarding.WorkItemId, status, ct, isTest);
                }

                return productOnboarding.PopulateProductOnboardingResponse(new() { Message = "Successfully synced product onboarding status." });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to sync product onboarding status");
                return new () { ResponseError = $"Failed to sync product onboarding: {ex.Message}" };
            }
        }
    }
}
