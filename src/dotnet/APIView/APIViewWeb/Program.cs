using System;
using Azure.Identity;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
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
                    IConfiguration settings = config.Build();
                    string endpoint = settings.GetValue<string>("AppConfigEndpoint");
                    // Load configuration from Azure App Configuration
                    config.AddAzureAppConfiguration(options =>
                    {
                        options.Connect(new Uri(endpoint), new DefaultAzureCredential()).ConfigureKeyVault(kv =>
                        {
                            kv.SetCredential(new DefaultAzureCredential());
                        });
                    });
                    config.AddUserSecrets(typeof(Program).Assembly);
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
