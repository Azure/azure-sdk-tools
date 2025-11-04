// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Hubbup.MikLabelModel;
using Azure.Identity;
using System;
using Azure.AI.OpenAI;
using OpenAI.Chat;
using Azure.Search.Documents.Indexes;
using Microsoft.Extensions.Configuration;
using Azure.Core;
using IssueLabelerService;
using Azure.Storage.Blobs;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) => {
        var functionConfig = context.Configuration;
        var configEndpoint = new Uri(functionConfig["ConfigurationEndpoint"]);
        var isRunningInAzure = functionConfig["IsRunningInAzure"] == "true";

        // Use appropriate credential based on environment
        TokenCredential credential = isRunningInAzure ? new ManagedIdentityCredential() : new DefaultAzureCredential();

        var builder = new ConfigurationBuilder();
        builder.AddAzureAppConfiguration(options =>
        {
            options.Connect(configEndpoint, credential);
        });

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

        services.AddSingleton<AzureOpenAIClient>(sp =>
        {
            var openAIEndpoint = new Uri(config.OpenAIEndpoint);
            
            var openAIClient = new AzureOpenAIClient(openAIEndpoint, credential);
            return openAIClient;
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

        services.AddSingleton<TriageRag>();
        services.AddSingleton<IModelHolderFactoryLite, ModelHolderFactoryLite>();
        services.AddSingleton<ILabelerLite, LabelerLite>();
        services.AddSingleton<LabelerFactory>();
        services.AddSingleton<AnswerFactory>();
        services.AddSingleton<IssueGeneratorFactory>();
    })
    .Build();

host.Run();
