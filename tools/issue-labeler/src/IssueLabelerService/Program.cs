// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.ClientModel.Primitives;
using Azure.Core;
using Azure.Identity;
using Azure.Search.Documents.Indexes;
using Azure.Storage.Blobs;
using Hubbup.MikLabelModel;
using IssueLabelerService;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenAI;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) => {
        var functionConfig = context.Configuration;
        var configEndpoint = new Uri(functionConfig["ConfigurationEndpoint"]);
        var isRunningInAzure = functionConfig["IsRunningInAzure"] == "true";

        // Use appropriate credential based on environment
        TokenCredential credential = isRunningInAzure 
            ? new ManagedIdentityCredential()
            : new ChainedTokenCredential(
                new AzureCliCredential(),
                new VisualStudioCredential(),
                new VisualStudioCodeCredential()
            );

        var builder = new ConfigurationBuilder();
        builder.AddAzureAppConfiguration(options =>
        {
            options.Connect(configEndpoint, credential);
        });
        builder.AddConfiguration(functionConfig);

        var configRoot = builder.Build();

        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        
        var configService = new Configuration(configRoot);
        services.AddSingleton<Configuration>(configService);
        
        var config = configService.GetDefault();

        // Need to combine the default config with the function app config
        // that way the function app doesn't yell at us.
        IConfiguration combinedConfig = new ConfigurationBuilder()
            .AddConfiguration(functionConfig)
            .AddConfiguration(configRoot.GetSection("defaults"))
            .Build();

        services.AddSingleton(combinedConfig);

        services.AddSingleton<OpenAIClient>(sp =>
        {
            return new OpenAIClient(
                new BearerTokenPolicy(
                    credential,
                    "https://ai.azure.com/.default"
                ),
                new OpenAIClientOptions
                {
                    Endpoint = new Uri($"{config.OpenAIEndpoint.TrimEnd('/')}/openai/v1/")
                }
            );
        });

        services.AddSingleton<BlobServiceClient>(sp =>
        {
            var blobServiceEndpoint = new Uri(config.BlobAccountUri);
            return new BlobServiceClient(blobServiceEndpoint, credential);
        });

        services.AddSingleton<SearchIndexClient>(sp =>
        {
            var searchEndpoint = new Uri(config.SearchEndpoint);
            return new SearchIndexClient(searchEndpoint, credential);
        });

        services.AddSingleton<TokenCredential>(credential);
        services.AddSingleton<TriageRag>();
        services.AddSingleton<IModelHolderFactoryLite, ModelHolderFactoryLite>();
        services.AddSingleton<ILabelerLite, LabelerLite>();
        services.AddSingleton<LabelerFactory>();
        services.AddSingleton<AnswerFactory>();
    })
    .Build();

host.Run();
