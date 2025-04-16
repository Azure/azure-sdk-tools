// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureSDKDevToolsMCP.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMcpServer().WithToolsFromAssembly();
var app = builder.Build();

// 👇 Map Mcp endpoints
app.MapMcp();

app.Run();