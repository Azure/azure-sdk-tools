using System;
using Azure.Identity;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Extensions.Logging;

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
                    IConfiguration settings = config.Build();
                    string appConfigUrl = settings.GetValue<string>("APPCONFIG_URL");
                    if(string.IsNullOrEmpty(appConfigUrl))
                    {
                        throw new InvalidOperationException("App Configuration URL is not set in APIView environment variable. This should be set using environment name APPCONFIG_URL and value 'https://<your-app-config-name>.azconfig.io'");
                    }
                    // Load configuration from Azure App Configuration
                    config.AddAzureAppConfiguration(options =>
                    {
                        options.Connect(new Uri(appConfigUrl), new DefaultAzureCredential()).ConfigureKeyVault(kv => 
                        { 
                            kv.SetCredential(new DefaultAzureCredential());
                        })
                        .Select(KeyFilter.Any)
                        .ConfigureRefresh(refresh =>
                        {
                            refresh.Register("Sentinel", refreshAll: true)
                                .SetRefreshInterval(TimeSpan.FromMinutes(5));
                        });
                    });
                    config.AddUserSecrets(typeof(Program).Assembly);
                })
                .ConfigureLogging(logging => {
                    logging.ClearProviders();
                    logging.AddConsole();
                    logging.AddDebug();
                    logging.AddApplicationInsights();
                    logging.SetMinimumLevel(LogLevel.Information);
                })
                .ConfigureKestrel(options =>
                {
                    // Set HTTP/1.1 and HTTP/2 as supported protocols
                    options.ConfigureEndpointDefaults(endpointOptions =>
                    {
                        endpointOptions.Protocols = HttpProtocols.Http1AndHttp2;
                    });
                })
                .UseStartup<Startup>();
    }
}
