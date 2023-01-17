using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PixelServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var webHostBuilder = new WebHostBuilder()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseStartup<Startup>()
                .UseDefaultServiceProvider(
                    (context, options) => options.ValidateScopes = context.HostingEnvironment.IsDevelopment())
                .UseKestrel();

            webHostBuilder.UseSockets(x => x.IOQueueCount = 2);
            webHostBuilder.UseApplicationInsights();
            webHostBuilder.Build().Run();
        }
    }
}
