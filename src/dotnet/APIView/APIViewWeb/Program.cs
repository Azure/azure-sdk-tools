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
                })
                .ConfigureKestrel(options =>
                {
                    options.Limits.MaxRequestBodySize = 100 * 1024 * 1024;
                })
                .UseStartup<Startup>();
    }
}
