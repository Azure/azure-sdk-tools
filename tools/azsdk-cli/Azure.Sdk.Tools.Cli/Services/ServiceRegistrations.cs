using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Services
{
    public static class ServiceRegistrations
    {    /// <summary>
         /// This is the function that defines all of the services available to any of the MCPTool instantiations. This
         /// same collection modification is run within the HostServerTool creation.
         /// </summary>
         /// <param name="services"></param>
         /// todo: make this use reflection to populate itself with all of our services and helpers
        public static void RegisterCommonServices(IServiceCollection services)
        {
            // perhaps we move this to a static function that we can call within HostTool as well.
            services.AddSingleton<IAzureService, AzureService>();
            //services.AddSingleton<IGitHubService, GitHubService>();
            //services.AddSingleton<IGitHelper, GitHelper>();
            //services.AddSingleton<ITypeSpecHelper, TypeSpecHelper>();
            //services.AddSingleton<IDevOpsConnection, DevOpsConnection>();
            //services.AddSingleton<IDevOpsService, DevOpsService>();
        }


    }
}
