// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Mock;
using Azure.Sdk.Tools.Mock.Handlers;

var builder = WebApplication.CreateBuilder(args);

// Bind Kestrel to loopback on a dynamic port so the mock server is truly stdio-only
// and doesn't conflict with other services or expose an unintended HTTP endpoint.
builder.WebHost.ConfigureKestrel(options =>
{
    options.Listen(System.Net.IPAddress.Loopback, 0);
});

// Suppress noisy framework logging — mock server only needs minimal output
builder.Logging.SetMinimumLevel(LogLevel.Warning);

// Register the mock tool factory (auto-discovers IMockToolHandler implementations)
builder.Services.AddSingleton<MockToolFactory>();

// Register mock MCP tools (same names/schemas as real CLI, but with mock responses)
MockToolRegistrations.RegisterMockMcpTools(builder.Services);

// Set up MCP server with stdio transport (same as the real CLI)
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport();

var app = builder.Build();

await app.RunAsync();
