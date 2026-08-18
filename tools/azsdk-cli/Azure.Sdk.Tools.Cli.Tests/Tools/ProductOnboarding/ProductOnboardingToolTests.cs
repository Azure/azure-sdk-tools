using Moq;
using Moq.Protected;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Notification;
using Azure.Sdk.Tools.Cli.Tests.Mocks.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.ProductOnboarding;
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.ProductOnboarding
{
    internal class ProductOnboardingToolTests
    {
        private ProductOnboardingTool productOnboardingTool;
        private IDevOpsService devOpsService;
        private TestLogger<ProductOnboardingTool> logger;

        [SetUp]
        public void Setup()
        {

            logger = new TestLogger<ProductOnboardingTool>();
            devOpsService = new MockDevOpsService();

            productOnboardingTool = new ProductOnboardingTool(devOpsService, logger);
        }

        private readonly static string MockProductId = "12345678-1234-5678-9012-123456789012";
        private readonly static string MockProductName = "Product Name";
        private readonly static string MockProductType = "SKU";
        private readonly static string MockProductLifecycle = "In Dev";
        private readonly static string MockServiceId = "87654321-4321-8765-1234-210987654321";
        private readonly static string MockServiceName = "Service Name";
        private readonly static bool MockNeedsSdk = true;
        private readonly static string MockDataPlane = "Yes";
        private readonly static string MockManagementPlane = "No";
        private readonly static string MockSubmitter = "@handle";

        [Test]
        public async Task Test_Update_ProductOnboarding_New()
        {
            var NonexistentId = MockProductId; // When productId == serviceId, mock service falls into "does not exist" path.

            var response = await productOnboardingTool.UpdateProductOnboarding(
                productId: Guid.Parse(NonexistentId),
                productName: MockProductName,
                productType: MockProductType,
                productLifecycle: MockProductLifecycle,
                serviceId: Guid.Parse(NonexistentId),
                serviceName: MockServiceName,
                needsSdk: MockNeedsSdk,
                dataPlane: MockDataPlane,
                managementPlane: MockManagementPlane,
                submitter: MockSubmitter,
                ct: default,
                isTest: true);
                
            Assert.IsNotNull(response);

            Assert.That(response.ProductOnboardingDetails?.ProductId, Is.EqualTo(NonexistentId));
            Assert.That(response.ProductOnboardingDetails?.ProductName, Is.EqualTo(MockProductName));
            Assert.That(response.ProductOnboardingDetails?.ProductType, Is.EqualTo(MockProductType));
            Assert.That(response.ProductOnboardingDetails?.ProductLifecycle, Is.EqualTo(MockProductLifecycle));
            Assert.That(response.ProductOnboardingDetails?.ServiceId, Is.EqualTo(NonexistentId));
            Assert.That(response.ProductOnboardingDetails?.ServiceName, Is.EqualTo(MockServiceName));
            Assert.That(response.ProductOnboardingDetails?.DataPlane, Is.EqualTo(MockDataPlane));
            Assert.That(response.ProductOnboardingDetails?.ManagementPlane, Is.EqualTo(MockManagementPlane));
            Assert.That(response.ProductOnboardingDetails?.Submitter, Is.EqualTo(MockSubmitter));
        }

        [Test]
        public async Task Test_Update_ProductOnboarding_Existing()
        {

            var response = await productOnboardingTool.UpdateProductOnboarding(
                productId: Guid.Parse(MockProductId),
                productName: MockProductName,
                productType: MockProductType,
                productLifecycle: MockProductLifecycle,
                serviceId: Guid.Parse(MockServiceId),
                serviceName: MockServiceName,
                needsSdk: MockNeedsSdk,
                dataPlane: MockDataPlane,
                managementPlane: MockManagementPlane,
                submitter: MockSubmitter,
                ct: default,
                isTest: true);

            Assert.IsNotNull(response);

            Assert.That(response.ProductOnboardingDetails?.ProductId, Is.EqualTo(MockProductId));
            Assert.That(response.ProductOnboardingDetails?.ProductName, Is.EqualTo(MockProductName));
            Assert.That(response.ProductOnboardingDetails?.ProductType, Is.EqualTo(MockProductType));
            Assert.That(response.ProductOnboardingDetails?.ProductLifecycle, Is.EqualTo(MockProductLifecycle));
            Assert.That(response.ProductOnboardingDetails?.ServiceId, Is.EqualTo(MockServiceId));
            Assert.That(response.ProductOnboardingDetails?.ServiceName, Is.EqualTo(MockServiceName));
            Assert.That(response.ProductOnboardingDetails?.DataPlane, Is.EqualTo(MockDataPlane));
            Assert.That(response.ProductOnboardingDetails?.ManagementPlane, Is.EqualTo(MockManagementPlane));
            Assert.That(response.ProductOnboardingDetails?.Submitter, Is.EqualTo(MockSubmitter));
        }
    }
}
