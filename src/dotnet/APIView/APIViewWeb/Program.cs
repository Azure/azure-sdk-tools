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
                    // Load configuration from Azure App Configuration
                    config.AddAzureAppConfiguration(options =>
                    {
                        options.Connect(connectionString).ConfigureKeyVault(kv => 
                        { 
                            kv.SetCredential(new DefaultAzureCredential());
                        });
                    });
                })
                .UseStartup<Startup>();
    }
}
