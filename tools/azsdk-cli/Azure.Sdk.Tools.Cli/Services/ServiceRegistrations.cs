// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Services
{
    public static class ServiceRegistrations
    {    /// <summary>
         /// This is the function that defines all of the services available to any of the MCPTool instantiations. This
         /// same collection modification is run within the HostServerTool::CreateAppBuilder.
         /// </summary>
         /// <param name="services"></param>
         /// todo: make this use reflection to populate itself with all of our services and helpers
        public static void RegisterCommonServices(IServiceCollection services)
        {
            // Service classes
            services.AddSingleton<IAzureService, AzureService>();
            services.AddSingleton<IAzureAgentServiceFactory, AzureAgentServiceFactory>();
            services.AddSingleton<IDevOpsService, DevOpsService>();
            services.AddSingleton<IGitHubService, GitHubService>();
            services.AddSingleton<IDevOpsConnection, DevOpsConnection>();

            // Helper classes
            services.AddSingleton<ILogAnalysisHelper, LogAnalysisHelper>();
            services.AddSingleton<IGitHelper, GitHelper>();
            services.AddSingleton<ITestHelper, TestHelper>();
            services.AddSingleton<ITypeSpecHelper, TypeSpecHelper>();
            services.AddSingleton<ISpecPullRequestHelper, SpecPullRequestHelper>();
            services.AddSingleton<IUserHelper, UserHelper>();
        }
    }
}
