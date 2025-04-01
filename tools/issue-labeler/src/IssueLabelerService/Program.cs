// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using AzureRagService;
using Hubbup.MikLabelModel;
using Azure.Identity;
using Azure.Search.Documents;
using System;
using Azure.AI.OpenAI;
using OpenAI.Chat;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) => {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        var config = context.Configuration;
        services.AddSingleton(config);

        services.AddSingleton<ChatClient>(sp =>
        {
            var openAIEndpoint = new Uri(config["OpenAIEndpoint"]);
            var credential = new DefaultAzureCredential();
            var openAIClient = new AzureOpenAIClient(openAIEndpoint, credential);
            var modelName = config["OpenAIModelName"];
            return openAIClient.GetChatClient(modelName);
        });

        services.AddSingleton<TriageRag>();
        services.AddSingleton<IModelHolderFactoryLite, ModelHolderFactoryLite>();
        services.AddSingleton<ILabelerLite, LabelerLite>();
    })
    .Build();

host.Run();
