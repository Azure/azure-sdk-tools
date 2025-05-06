using AzureSDKDSpecTools.Models;
using AzureSDKDSpecTools.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.TestManagement.WebApi;

namespace AzureSDKSpecToolTests
{
    public class ReleasePlanTest
    {
        private IDevOpsService devOpsService;
        public ReleasePlanTest()
        {

            var serviceProvider = new ServiceCollection()
             .AddLogging(configure => configure.AddConsole())
             .BuildServiceProvider();
            var logger = serviceProvider.GetService<ILogger<DevOpsService>>();
            devOpsService = new DevOpsService(logger);
        }

        [Fact]
        public async Task GetReleasePlanTest()
        {
            var releasePlan = await devOpsService.GetReleasePlan(26648);
            Assert.NotNull(releasePlan);
            Assert.Equal(26648, releasePlan.WorkItemId);
            Assert.True(!string.IsNullOrEmpty(releasePlan.Title));
        }

        [Fact]
        public async Task GetReleasePlansForServicePrductTest()
        {
            var pr = "https://github.com/Azure/azure-rest-api-specs/pull/32282";
            var releasePlans = await devOpsService.GetReleasePlans(Guid.Parse("42815c77-2fba-4eb9-b052-5f0c545cedf3"), Guid.Parse("8218fbb5-917d-4cd7-8498-9f21b189e231"), pr);
            Assert.NotNull(releasePlans);
            Assert.NotEmpty(releasePlans);
            Assert.Contains(pr.ToLower(), releasePlans.First().SpecPullRequests);
        }

        [Fact (Skip = "Disabled for default run test since this creates a work item")]
        public async Task CreateReleasePlanTest()
        {
            var pr = "https://github.com/Azure/azure-rest-api-specs/pull/32282";
            var service = "42815c77-2fba-4eb9-b052-5f0c545cedf3";
            var product = "8218fbb5-917d-4cd7-8498-9f21b189e231";

            var releasePlan = new ReleasePlan()
            {
                ServiceTreeId = service,
                ProductTreeId = product,
                SDKReleaseMonth = "May 2025",
                ProductName = "Release Planner Test",
                IsManagementPlane = true,
                IsDataPlane = false,
                SpecPullRequests = [pr],
                SpecType = "TypeSpec"
            };
            var workItem = await devOpsService.CreateReleasePlanWorkItem(releasePlan);
            Assert.NotNull(workItem);
            Assert.NotEmpty(workItem.Url);
        }
    }
}
