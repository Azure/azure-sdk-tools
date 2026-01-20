// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace IssueLabelerService
{
    /// <summary>
    /// Provides tool label descriptions for MCP issue classification.
    /// </summary>
    internal static class McpToolDescription
    {
        /// <summary>
        /// Static dictionary of tool label descriptions.
        /// This provides semantic context to the LLM about what each label means.
        /// </summary>
        public static Dictionary<string, string> ToolDescriptions { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Infrastructure & Core
            ["tools-Core"] = "MCP server infrastructure and system-wide behavior: startup/shutdown, protocol (stdio, JSON-RPC), consolidated mode, CLI framework, cross-cutting issues affecting MULTIPLE tools, tool routing/dispatch when MULTIPLE tools affected, parameter handling affecting multiple tools, help content generation, new framework capabilities, MCP client behavior issues (e.g., tool call loops, repeated invocations). Use for [CONSOLIDATED] issues. NOT for single tool's implementation bugs - if ONE tool returns wrong data, use that tool's label. NOT for tool-specific output size/truncation/formatting issues - use the specific tool label (e.g., tools-ARM for subscription list output issues). NOT for Azure AI Foundry agents (use tools-Foundry).",
            
            // Authentication & Security
            ["tools-Auth"] = "Authentication framework: credential configuration, credential types (DefaultAzureCredential, ChainedTokenCredential, ManagedIdentityCredential), login flows, RBAC framework (local), auth documentation. NOT tool-specific auth errors that occur after tool invocation, NOT remote RBAC (use remote-mcp).",
            
            // Packaging & Distribution
            ["tools-npx"] = "npm/npx packaging: @azure/mcp package, npm install issues, node_modules, JavaScript/TypeScript packaging problems. NOT general installation UX.",
            ["tools-NuGet"] = ".NET/NuGet packaging: dotnet tool install, NuGet gallery listings, .nupkg files, NuGet README, server.json in NuGet packages, CONTRIBUTING.md for NuGet.",
            ["tools-Docker"] = "Docker containers: Docker images, Dockerfile, container runtime, mcr.microsoft.com registry, Docker-specific auth or startup issues, Microsoft Artifact Registry branding.",
            ["tools-Setup"] = "MCP server installation experience: first-run MCP setup, bootstrap workflows, MCP installation UX (not npx/NuGet/Docker specific), new MCP package formats (uvx/Python, maven/Java). NOT Azure CLI/azd tool features (use tools-AzCLI or tools-Azd).",
            ["tools-McpRegistry"] = "MCP catalogs & discovery: marketplace listings (Cline, Glama, mcpservers.org), server.json schema for registry, registry metadata, catalog badges, pipeline updates for server.json generation.",
            ["tools-VSIX"] = "VS Code extension: extension marketplace, VSIX packaging, extension host issues, VS Code-specific functionality, extension installation/setup problems, server availability in extension. ANY issue about the VS Code extension goes here, even if it mentions 'install' or 'setup'.",
            // Observability
            ["tools-Telemetry"] = "Telemetry & metrics: OpenTelemetry integration, metrics collection, tracing, telemetry events, ToolArea settings, version strings in telemetry.",
            ["tools-Observability"] = "Debug logging & diagnostics: verbose logs, debug-quality logs, server diagnostics, troubleshooting output, retry logging.",

            // Remote & Deployment
            ["remote-mcp"] = "Remote MCP deployment: ACA/App Service hosting, HTTP transport, OAuth for remote endpoints specifically, CORS, rate limiting, azd templates for remote deployment, AI Foundry CONNECTION issues (can't connect to MCP), managed identity for remote hosting, RBAC for remote scenarios. Issues with [Remote] tag.",
            ["tools-Deploy"] = "Deployment tooling: deployment plans, IaC rules/guidelines, app logs from deployed apps, CI/CD pipeline guidance, architecture diagram generation, Bicep/Terraform scaffolding. NOT remote MCP infrastructure (use remote-mcp).",

            // Azure Services - Compute
            ["tools-Aks"] = "Azure Kubernetes Service: AKS clusters, kubectl, Kubernetes deployments.",
            ["tools-ACR"] = "Azure Container Registry: container image management, ACR authentication, registry operations.",
            ["tools-AppService"] = "Azure App Service: web apps, app service plans, deployment slots.",
            ["tools-FunctionApp"] = "Azure Functions: function apps, function app resources, triggers, bindings, Functions runtime, function management.",
            ["tools-VirtualDesktop"] = "Azure Virtual Desktop & VMs: virtual machines, VM management.",

            // Azure Services - Data
            ["tools-CosmosDB"] = "Azure Cosmos DB: document databases, Cosmos queries, Cosmos SDK issues.",
            ["tools-SQL"] = "Azure SQL tool: SQL databases, SQL Server, SQL queries, SQL tool implementation, SQL code refactoring, SQL .NET SDK integration. If [SQL] tag in title or 'SQL' appears prominently → use this label.",
            ["tools-Postgres"] = "Azure Database for PostgreSQL: Postgres databases, Postgres connections.",
            ["tools-MySQL"] = "Azure Database for MySQL: MySQL Flexible Server, MySQL databases, SQL queries, table schemas, server parameters.",
            ["tools-Redis"] = "Azure Cache for Redis: Redis cache, Redis clusters.",
            ["tools-Kusto"] = "Azure Data Explorer (Kusto): KQL queries, Kusto clusters, ADX.",
            ["tools-Storage"] = "Azure Storage: blobs, queues, tables, file shares, storage accounts.",

            // Azure Services - AI & Analytics
            ["tools-Foundry"] = "Azure AI services, Azure AI Foundry, AI agents, Cognitive Services, AI model deployments, OpenAI integration. The umbrella tool for general Azure AI functionality.",
            ["tools-Search"] = "Azure AI Search: search indexes, cognitive search, vector search.",
            ["tools-DocumentIntelligence"] = "Azure AI Document Intelligence & Vision: document processing, OCR, form recognition.",
            ["tools-Speech"] = "Azure AI Speech: speech-to-text, text-to-speech, speech services.",
            ["tools-ApplicationInsights"] = "Application Insights: app monitoring, app traces (apptrace), performance monitoring.",
            ["tools-Monitor"] = "Azure Monitor: Log Analytics, metrics, alerts, health models, workbooks. NOT Application Insights specific features.",

            // Azure Services - Messaging
            ["tools-EventGrid"] = "Azure Event Grid: event topics, event subscriptions, event delivery.",
            ["tools-EventHubs"] = "Azure Event Hubs: event streaming, consumer groups, event hubs namespaces.",
            ["tools-ServiceBus"] = "Azure Service Bus: queues, topics, subscriptions, messaging.",
            ["tools-WebPubSub"] = "Azure Web PubSub: WebSocket messaging, pub/sub.",

            // Azure Services - Security & Management
            ["tools-KeyVault"] = "Azure Key Vault: secrets, keys, certificates, vault management.",
            ["tools-AppConfig"] = "Azure App Configuration: feature flags, configuration settings.",
            ["tools-Policy"] = "Azure Policy: policy assignments, policy definitions, enforcement modes, policy compliance analysis, governance.",

            // Azure Services - Integration
            ["tools-ARM"] = "ARM SDK tools for listing/managing subscriptions, resource groups, Azure VMs/VMSS, general Azure Compute services. Includes subscription_list, resource listing tools, and ANY issues with their output. NOT for az CLI command issues (use tools-AzCLI). NOT for service-specific tool refactoring - if issue has [SQL], [CosmosDB], [Storage] etc. tag, use that service's label.",
            ["tools-Azd"] = "Azure Developer CLI (azd) tool module: azd commands, azd templates, azd environments, Aspire, adding azd features. NOT remote deployment (use remote-mcp).",
            ["tools-AzCLI"] = "Azure CLI (az) tool module: az commands, az login, CLI extensions, PowerShell cmdlets, CLI installation helpers within MCP. Includes issues with az CLI command output. NOT MCP server setup (use tools-Setup). NOT for ARM SDK subscription listing tools (use tools-ARM).",

            // Azure Services - Other
            ["tools-Marketplace"] = "Azure Marketplace: marketplace products, marketplace offers, product discovery, offer management.",
            ["tools-LoadTesting"] = "Azure Load Testing: load tests, test runs, performance testing.",
            ["tools-Grafana"] = "Azure Managed Grafana: Grafana dashboards, visualization.",
            ["tools-Datadog"] = "Datadog integration: Datadog monitoring, Datadog API.",
            ["tools-ConfidentialLedger"] = "Azure Confidential Ledger: ledger operations, secure logging.",
            ["tools-Communication"] = "Azure Communication Services: SMS, email, voice, chat.",

            // Specialized
            ["tools-BestPractices"] = "Best practices tooling: guidance recommendations, policy checks, best practices documentation, get_azure_best_practices FUNCTIONALITY issues. Best practices is ALWAYS a separate tool - never merge with other labels",
            ["tools-Terraform"] = "Terraform best practices, Terraform tool descriptions.",
            ["tools-bicep"] = "Bicep integration: Bicep schema, Bicep generation.",
            ["tools-Startups"] = "Azure for Startups: startup guidance, startup-specific tooling.",
            ["tools-cloudarchitect"] = "Cloud Architect tool: architecture recommendations, design guidance.",
            ["tools-CopilotNetwork"] = "Network Copilot: network diagnostics, connectivity analysis.",
            ["tools-ISV"] = "ISV integrations: third-party vendor tools, partner integrations. NOT Datadog (use tools-Datadog).",
            ["tools-IntelliJ"] = "IntelliJ plugin: JetBrains IDE integration.",
            ["tools-eclipse"] = "Eclipse plugin: Eclipse IDE integration.",
            ["tools-Workbooks"] = "Azure Workbooks: workbook templates, workbook queries.",
            ["tools-ResourceHealth"] = "Azure Resource Health: resource availability status, health diagnostics, historical health information, service health events, troubleshooting resource issues.",
            ["tools-AppLens"] = "AppLens diagnostics: diagnose Azure resource issues, troubleshoot problems/errors, investigate resource health, find root causes, get remediation recommendations, analyze performance/availability/reliability issues.",
            ["tools-managedlustre"] = "Azure Managed Lustre: Lustre file systems, HPC storage.",
            ["tools-SignalR"] = "Azure SignalR Service: get/list SignalR runtime details, SignalR configuration, network ACLs, upstream templates, SignalR identity settings.",
        };
    }
}