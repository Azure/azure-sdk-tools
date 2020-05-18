using Azure.Cosmos;
using Azure.Sdk.Tools.PipelineWitness;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Text;

[assembly: FunctionsStartup(typeof(Startup))]

namespace Azure.Sdk.Tools.PipelineWitness
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            builder.Services.AddLogging();
            builder.Services.AddMemoryCache();
            builder.Services.AddSingleton<RunProcessor>();
            builder.Services.AddHttpClient();
        }
    }
}
