using Azure.Identity;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace APIViewWeb
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateWebHostBuilder(args).Build().Run();
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    config.AddEnvironmentVariables(prefix: "APIVIEW_");
                    config.AddUserSecrets(typeof(Program).Assembly);
                    IConfiguration settings = config.Build();
                    string connectionString = settings.GetValue<string>("APPCONFIG");
                    string clientID = settings.GetValue<string>("APPCONFIG_CLIENT_ID");
                    string tenantId = settings.GetValue<string>("APPCONFIG_TENANT_ID");
                    string clientSecret = settings.GetValue<string>("APPCONFIG_CLIENT_SECRET");
                    // Load configuration from Azure App Configuration
                    config.AddAzureAppConfiguration(options =>
                    {
                        options.Connect(connectionString).ConfigureKeyVault(kv => 
                        { 
                            kv.SetCredential(new ClientSecretCredential(tenantId, clientID, clientSecret));
                        });
                    });
                })
                .UseStartup<Startup>();
    }
}
