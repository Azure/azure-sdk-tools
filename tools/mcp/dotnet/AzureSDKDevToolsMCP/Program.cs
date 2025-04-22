// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureSDKDevToolsMCP.Tools;
using AzureSDKDevToolsMCP.Helpers;
using AzureSDKDevToolsMCP.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;
using AzureSDKDSpecTools.Services;


var builder = Host.CreateApplicationBuilder(args);


#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
builder.Services
 .AddMcpServer()
 .WithStdioServerTransport()
 .WithToolsFromAssembly();
#pragma warning restore IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code

builder.Services.AddSingleton<IGitHubService, GitHubService>();
builder.Services.AddSingleton<IGitHelper, GitHelper>();
builder.Services.AddSingleton<IDevOpsService, DevOpsService>();
await builder.Build().RunAsync();
