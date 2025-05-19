// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureSDKDevToolsMCP.Tools;
using AzureSDKDevToolsMCP.Helpers;
using AzureSDKDevToolsMCP.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;
using AzureSDKDSpecTools.Services;
using AzureSDKDSpecTools.Helpers;
using Microsoft.Extensions.Logging;


var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddLogging(configure => configure.AddConsole());
builder.Services.AddSingleton<IGitHubService, GitHubService>();
builder.Services.AddSingleton<IGitHelper, GitHelper>();
builder.Services.AddSingleton<ITypeSpecHelper, TypeSpecHelper>();
builder.Services.AddSingleton<ISpecPullRequestHelper, SpecPullRequestHelper>();
builder.Services.AddSingleton<IDevOpsConnection, DevOpsConnection>();
builder.Services.AddSingleton<IDevOpsService, DevOpsService>();


#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
builder.Services
 .AddMcpServer()
 .WithStdioServerTransport()
 .WithToolsFromAssembly();
#pragma warning restore IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code


await builder.Build().RunAsync();
