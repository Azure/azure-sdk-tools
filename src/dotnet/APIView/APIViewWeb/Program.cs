using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
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
                    // This should add an environment variable containing the Azure blob storage connection string via APIVIEW_STORAGE.
                    config.AddEnvironmentVariables(prefix: "APIVIEW_");
                    config.AddUserSecrets(typeof(Program).Assembly);
                })
                .UseStartup<Startup>();
    }
}
