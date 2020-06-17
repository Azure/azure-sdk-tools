using Azure.Cosmos;
using Azure.Sdk.Tools.PipelineWitness;
using Azure.Sdk.Tools.PipelineWitness.Services.FailureAnalysis;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
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
            builder.Services.AddSingleton<IFailureAnalyzer, FailureAnalyzer>();
            builder.Services.AddSingleton<IFailureClassifier, CancelledTaskClassifier>();
            builder.Services.AddSingleton<IFailureClassifier, CosmosDbEmulatorStartFailureClassifier>();
            builder.Services.AddSingleton<IFailureClassifier, AzurePipelinesPoolOutageClassifier>();
            builder.Services.AddSingleton<IFailureClassifier, PythonPipelineTestFailureClassifier>();
            builder.Services.AddSingleton<IFailureClassifier, JavaScriptLiveTestFailureClassifier>();
            builder.Services.AddSingleton<IFailureClassifier, TestResourcesDeploymentFailureClassifier>();
            builder.Services.AddSingleton<IFailureClassifier, DotnetPipelineTestFailureClassifier>();
            builder.Services.AddSingleton<IFailureClassifier, JavaPipelineTestFailureClassifier>();

            // POSSIBLE WORKAROUND: The Azure Functions host environment has a health check
            //                      which pulls down the host if it exceeds 300 active outbound
            //                      connections within a 1 second period.
            //
            //                      By more aggressively timing out handlers we might be able to
            //                      work around this but at this point it is just a hypothesis.
            builder.Services.AddHttpClient<RunProcessor>().SetHandlerLifetime(TimeSpan.FromSeconds(60));
        }
    }
}
