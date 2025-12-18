using System.Text.Json;

namespace IssueLabelerService.Tests
{
    /// <summary>
    /// Ground truth dataset for MCP labeler evaluation
    /// </summary>
    public static class McpGroundTruthDataset
    {
        /// <summary>
        /// Get the full test dataset with ground truth labels
        /// </summary>
        public static List<McpTestCase> GetTestCases()
        {
            return new List<McpTestCase>
            {
                // Blob Storage issues
                new McpTestCase
                {
                    IssueNumber = 1,
                    Title = "Blob storage tool returns 404 error when listing containers",
                    Body = "When using the MCP server to list containers, I get a 404 error:\n\nError: Container not found\nStatusCode: 404\nErrorCode: ContainerNotFound\n\nI've verified that:\n- The storage account exists\n- The container name is correct\n- Permissions are set correctly with Storage Blob Data Reader role\n\nThis works fine when using Azure Portal or Azure CLI, but fails through the MCP storage tool.",
                    ExpectedServerLabel = "server-Azure.Mcp",
                    ExpectedToolLabel = "tools-Storage",
                    Notes = "Clear blob storage tool issue"
                },

                // Telemetry initialization issue - Fabric server
                new McpTestCase
                {
                    IssueNumber = 101,
                    Title = "Server stops shortly after startup due to telemetry initialization failure",
                    Body = "When running the MCP server (using the Fabric server configuration), the process exits a few seconds after startup with the following error:\n\nSystem.InvalidOperationException: Telemetry service has not been initialized. Use InitializeAsync() before any other operations.\n at Azure.Mcp.Core.Services.Telemetry.TelemetryService.CheckInitialization()\n at Azure.Mcp.Core.Services.Telemetry.TelemetryService.StartActivity()\n at Azure.Mcp.Core.Commands.CommandFactory.Execute()\n\nThis happens even when following the standard setup steps.\n\nI have confirmed:\n- The Fabric MCP server was set up according to the installation guide\n- The environment uses .NET SDK 9.0.305 on Windows 11\n- No custom configuration overrides were applied\n\nExpected behavior: The server should start normally without requiring manual telemetry initialization.",
                    ExpectedServerLabel = "server-Fabric.Mcp",
                    ExpectedToolLabel = "tools-Telemetry",
                    Notes = "Fabric server telemetry initialization issue"
                },

                // Key Vault issues
                new McpTestCase
                {
                    IssueNumber = 2,
                    Title = "Authentication fails when connecting to Azure Key Vault via MCP",
                    Body = "I'm trying to use the MCP Key Vault tool to retrieve secrets, but authentication consistently fails with:\n\nAzure.Identity.CredentialUnavailableException: DefaultAzureCredential failed to retrieve a token\n\nSteps to reproduce:\n1. Configure MCP server with Key Vault tool enabled\n2. Set AZURE_KEY_VAULT_URL environment variable\n3. Attempt to retrieve a secret using the get-secret tool\n4. Authentication fails\n\nI've verified the Key Vault exists and I have 'Key Vault Secrets User' role assigned.",
                    ExpectedServerLabel = "server-Azure.Mcp",
                    ExpectedToolLabel = "tools-KeyVault",
                    Notes = "Key Vault authentication issue"
                },

                // ARM/Resource Management issues
                new McpTestCase
                {
                    IssueNumber = 3,
                    Title = "MCP ARM tool fails to list resources in subscription",
                    Body = "When using the Azure Resource Manager tool to list resources, I get an error:\n\nError: Failed to list resources\nThe client does not have authorization to perform action 'Microsoft.Resources/subscriptions/resources/read'\n\nI'm authenticated with Azure CLI and have Contributor role on the subscription. The same query works with 'az resource list'.",
                    ExpectedServerLabel = "server-Azure.Mcp",
                    ExpectedToolLabel = "tools-Arm",
                    Notes = "ARM resource listing issue"
                },

                // Cosmos DB issues
                new McpTestCase
                {
                    IssueNumber = 4,
                    Title = "Cosmos DB tool timeout when querying large collections",
                    Body = "The MCP Cosmos DB tool times out when querying collections with more than 10k documents:\n\nError: Request timeout\nOperation timed out after 30000ms\n\nQuery: SELECT * FROM c WHERE c.status = 'active'\n\nThe same query works fine in Data Explorer. Is there a way to increase the timeout or enable pagination?",
                    ExpectedServerLabel = "server-Azure.Mcp",
                    ExpectedToolLabel = "tools-CosmosDb",
                    Notes = "Cosmos DB timeout issue"
                },

                // General MCP server issues (no specific tool)
                new McpTestCase
                {
                    IssueNumber = 5,
                    Title = "MCP server crashes on startup with TypeScript error",
                    Body = "The MCP server fails to start with the following error:\n\nTypeError: Cannot read property initialize of undefined at Server.start (server.ts:45)\n\nEnvironment:\n- Node.js v18.17.0\n- MCP Server v1.0.0\n- OS: macOS 14.0\n\nThis started happening after updating to the latest version.",
                    ExpectedServerLabel = "server-Azure.Mcp",
                    ExpectedToolLabel = "tools-Core",
                    Notes = "General server issue, not tool-specific"
                },

                // SQL Database issues
                new McpTestCase
                {
                    IssueNumber = 6,
                    Title = "Azure SQL tool connection string parsing error",
                    Body = "When configuring the SQL database tool, the connection string is not parsed correctly:\n\nError: Invalid connection string format\nParameter 'Server' is required\n\nConnection string format used:\nServer=tcp:myserver.database.windows.net,1433;Database=mydb;Authentication=Active Directory Default;\n\nThis is the standard Azure SQL connection string format.",
                    ExpectedServerLabel = "server-Azure.Mcp",
                    ExpectedToolLabel = "tools-Sql",
                    Notes = "SQL connection string issue"
                },

                // App Configuration issues
                new McpTestCase
                {
                    IssueNumber = 7,
                    Title = "App Configuration tool fails to retrieve feature flags",
                    Body = "The MCP App Configuration tool can retrieve key-values but fails when trying to get feature flags:\n\nError: Feature flag 'MyFeature' not found\nStatus: 404\n\nThe feature flag exists in the App Configuration store and is accessible via Azure Portal. Regular key-values work fine.",
                    ExpectedServerLabel = "server-Azure.Mcp",
                    ExpectedToolLabel = "tools-AppConfig",
                    Notes = "App Configuration feature flags issue"
                },

                // Service Bus issues
                new McpTestCase
                {
                    IssueNumber = 8,
                    Title = "Service Bus tool message peek returns empty array",
                    Body = "When using the Service Bus tool to peek messages from a queue, it always returns an empty array even though messages are visible in Azure Portal:\n\nconst messages = await serviceBus.peekMessages('myqueue', 10);\nconsole.log(messages); // []  \n\nQueue has 100+ messages. Using Receiver role for authentication.",
                    ExpectedServerLabel = "server-Azure.Mcp",
                    ExpectedToolLabel = "tools-ServiceBus",
                    Notes = "Service Bus message peek issue"
                },

                // Event Grid issues
                new McpTestCase
                {
                    IssueNumber = 9,
                    Title = "Event Grid tool subscription creation fails with validation error",
                    Body = "Creating an event subscription via the MCP Event Grid tool fails:\n\nError: Event subscription validation failed\nThe webhook endpoint did not respond with a valid validation response\n\nThe webhook endpoint is publicly accessible and responds correctly to manual tests. Event type filter: Microsoft.Storage.BlobCreated",
                    ExpectedServerLabel = "server-Azure.Mcp",
                    ExpectedToolLabel = "tools-EventGrid",
                    Notes = "Event Grid subscription validation issue"
                },

                // Monitoring/Application Insights issues
                new McpTestCase
                {
                    IssueNumber = 10,
                    Title = "Application Insights tool query returns incorrect time range",
                    Body = "When querying Application Insights data with the MCP monitoring tool, the time range filter is not applied correctly:\n\nQuery: requests | where timestamp > ago(1h)\nResult: Returns data from the past 24 hours instead of 1 hour\n\nThe same query works correctly in Azure Portal Log Analytics.",
                    ExpectedServerLabel = "server-Azure.Mcp",
                    ExpectedToolLabel = "tools-Monitoring",
                    Notes = "Application Insights query issue"
                },

                // Multiple tools mentioned (should pick primary)
                new McpTestCase
                {
                    IssueNumber = 11,
                    Title = "Error when copying blob to Cosmos DB via MCP tools",
                    Body = "I'm trying to copy data from Blob Storage to Cosmos DB using MCP tools, but the operation fails:\n\n1. Download blob using storage tool - works fine\n2. Parse JSON content - works fine  \n3. Insert into Cosmos DB - fails with 'Partition key missing'\n\nThe blob data includes the partition key field. Is this a known limitation when chaining tools?",
                    ExpectedServerLabel = "server-Azure.Mcp",
                    ExpectedToolLabel = "tools-CosmosDb",
                    Notes = "Multiple tools mentioned, but Cosmos is the failure point"
                },

                // Documentation/feature request (should still get server label)
                new McpTestCase
                {
                    IssueNumber = 12,
                    Title = "Add support for Azure Functions in MCP",
                    Body = "It would be great to have an MCP tool for managing Azure Functions:\n\n- List functions in a function app\n- Invoke functions with test payloads\n- View function logs\n- Update function settings\n\nThis would complement the existing ARM tool nicely.",
                    ExpectedServerLabel = "server-Azure.Mcp",
                    ExpectedToolLabel = "tools-FunctionApp",
                    Notes = "Feature request - no existing tool involved"
                },

                // Ambiguous tool (blob vs file share)
                new McpTestCase
                {
                    IssueNumber = 13,
                    Title = "Cannot access Azure Files share through storage tool",
                    Body = "The MCP storage tool seems to only work with Blob Storage. When I try to access an Azure Files share, I get:\n\nError: Resource type not supported\n\nIs Azure Files supported, or is this only for Blob Storage?",
                    ExpectedServerLabel = "server-Azure.Mcp",
                    ExpectedToolLabel = "tools-Storage",
                    Notes = "Storage tool - Files vs Blobs clarification needed"
                },

                // Configuration issue - Core MCP functionality
                new McpTestCase
                {
                    IssueNumber = 14,
                    Title = "Environment variables not loaded when MCP server starts",
                    Body = "Environment variables defined in .env file are not being loaded by the MCP server:\n\nAZURE_TENANT_ID=xxx\nAZURE_CLIENT_ID=yyy  \nAZURE_CLIENT_SECRET=zzz\n\nThe server starts but authentication fails because these values are undefined. Do I need to explicitly load them?",
                    ExpectedServerLabel = "server-Azure.Mcp",
                    ExpectedToolLabel = "tools-Core",
                    Notes = "Core MCP server configuration issue"
                },

                // Network/connectivity issue
                new McpTestCase
                {
                    IssueNumber = 15,
                    Title = "All MCP tools fail with network timeout in corporate environment",
                    Body = "Running MCP server behind corporate proxy causes all Azure tools to timeout:\n\nError: connect ETIMEDOUT\n\nI've set HTTP_PROXY and HTTPS_PROXY environment variables. Azure CLI works fine with the same proxy settings. Is there additional proxy configuration needed for MCP?",
                    ExpectedServerLabel = "server-Azure.Mcp",
                    ExpectedToolLabel = "tools-Core",
                    Notes = "Network/proxy configuration issue affecting all tools"
                },

                        // 16 — TRICKY: Fabric issue that *mentions Azure* heavily
        new McpTestCase
        {
            IssueNumber = 16,
            Title = "Fabric server throws telemetry exception when running Azure workspace sample",
            Body = "Running Fabric MCP server using the Azure quickstart sample throws:\n\nSystem.InvalidOperationException: Telemetry service not initialized.\n\nBecause the sample contains Azure references, it's easy to misclassify this as Azure.Mcp.\n\nExpected: Fabric.Mcp + tools-Telemetry.",
            ExpectedServerLabel = "server-Fabric.Mcp",
            ExpectedToolLabel = "tools-Telemetry",
            Notes = "Tricky Fabric-vs-Azure confusion case"
        },

        // 17 — TRICKY: Issue looks like Compute but is actually Core
        new McpTestCase
        {
            IssueNumber = 17,
            Title = "MCP server never responds after agent connects",
            Body = "When Copilot connects to MCP, the server does not send the capabilities list and hangs.\nNo tools are executed yet.\nThis is before any Azure tool runs.",
            ExpectedServerLabel = "server-Azure.Mcp",
            ExpectedToolLabel = "tools-Core",
            Notes = "Looks like a tool issue but is actually Core server initialization"
        },

        // 18 — TRICKY: Mentions two tools but failure belongs to the second
        new McpTestCase
        {
            IssueNumber = 18,
            Title = "Storage download works but inserting into SQL fails",
            Body = "Workflow:\n1. Downloaded blob using Storage tool → OK\n2. Insert into SQL table using MCP SQL tool → fails:\nError: Column 'id' cannot be null.\n\nEven though Storage is mentioned first, SQL is the actual failing tool.",
            ExpectedServerLabel = "server-Azure.Mcp",
            ExpectedToolLabel = "tools-Sql",
            Notes = "Multi-tool test; failure is SQL"
        },

        // 19 — TRICKY: Authentication error that AIs often attribute to Key Vault
        new McpTestCase
        {
            IssueNumber = 19,
            Title = "MCP fails to authenticate using DefaultAzureCredential",
            Body = "DefaultAzureCredential fails:\nCredentialUnavailableException: ManagedIdentityCredential authentication failed.\nThis happens before ANY Key Vault / Storage / ARM call.\n\nExpected: This is a Core auth issue, not a tool issue.",
            ExpectedServerLabel = "server-Azure.Mcp",
            ExpectedToolLabel = "tools-Auth",
            Notes = "Ambiguous auth error; classifier must not guess a tool"
        },

        // 21 — TRICKY: Agent incorrectly defaults to az CLI
        new McpTestCase
        {
            IssueNumber = 21,
            Title = "Agent keeps using 'az' instead of MCP tools",
            Body = "Regardless of request, the GitHub Copilot agent always chooses Azure CLI:\nUser: 'List my resource groups'\nAgent: runs 'az group list'\nExpected: Use MCP ARM tool.",
            ExpectedServerLabel = "server-Azure.Mcp",
            ExpectedToolLabel = "tools-Core",
            Notes = "Agent routing problem; not ARM tool failure"
        },

        // 22 — TRICKY: Looks like Storage but is actually Search
        new McpTestCase
        {
            IssueNumber = 22,
            Title = "Search tool returns empty array for valid index with blob-backed data",
            Body = "Index is populated using blobs as input. User thinks storage is failing, but the problem is:\n\nError: search='*' returned 0 documents\nStorage layer works fine.\nSearch tool misconfiguration caused missing fields.",
            ExpectedServerLabel = "server-Azure.Mcp",
            ExpectedToolLabel = "tools-Search",
            Notes = "Misleading storage reference"
        },

        // 23 — TRICKY: Cosmos error that looks like Storage
        new McpTestCase
        {
            IssueNumber = 23,
            Title = "Blob content migration fails with PartitionKeyMissing in Cosmos DB",
            Body = "Workflow:\n1. Download blob → OK\n2. Insert into Cosmos → fails:\nError: PartitionKeyMissing\nThis failure belongs to Cosmos, not Storage.",
            ExpectedServerLabel = "server-Azure.Mcp",
            ExpectedToolLabel = "tools-CosmosDb",
            Notes = "Looks like blob problem but is Cosmos"
        },

        // 24 — TRICKY: Fabric tool behaving like Azure core
        new McpTestCase
        {
            IssueNumber = 24,
            Title = "Fabric server child host fails to forward capability list",
            Body = "Child host started with --namespace returns:\nError: CapabilityNotSupported\nThis happens only under Fabric configuration.",
            ExpectedServerLabel = "server-Fabric.Mcp",
            ExpectedToolLabel = "tools-Core",
            Notes = "Fabric capability forwarding bug"
        },

        // 25 — Redis SSL
        new McpTestCase
        {
            IssueNumber = 25,
            Title = "Redis tool throws SSLHandshakeFailed",
            Body = "Connecting to Azure Cache for Redis fails:\nSSLHandshakeFailed: certificate not trusted.\nStandard Redis TLS 1.2 enabled.",
            ExpectedServerLabel = "server-Azure.Mcp",
            ExpectedToolLabel = "tools-Redis",
            Notes = "Redis TLS error"
        },

        // 26 — Event Hubs receiver
        new McpTestCase
        {
            IssueNumber = 26,
            Title = "Event Hubs tool consumer group read fails",
            Body = "Error: ReceiverDisconnectedError\nEntity was not deleted; works in Azure CLI.",
            ExpectedServerLabel = "server-Azure.Mcp",
            ExpectedToolLabel = "tools-EventHubs",
            Notes = "Event Hubs failure"
        },

        // 27 — Kusto tool ingestion error
        new McpTestCase
        {
            IssueNumber = 27,
            Title = "Kusto tool cannot ingest JSON document",
            Body = "Error: Mapping reference not found.\nInput file stored in blob.\nThis is not a storage issue.",
            ExpectedServerLabel = "server-Azure.Mcp",
            ExpectedToolLabel = "tools-Kusto",
            Notes = "Kusto ingestion issue"
        },

        // 28 — TRICKY: Function Apps vs AppService confusion
        new McpTestCase
        {
            IssueNumber = 28,
            Title = "Function app invocation fails through MCP",
            Body = "Calling function returns:\nError 500: Host not running.\nUser suspects AppService but this is a Function App scenario.",
            ExpectedServerLabel = "server-Azure.Mcp",
            ExpectedToolLabel = "tools-FunctionApp",
            Notes = "AppService vs FunctionApps ambiguity"
        },

        // 29 — SQL vs Postgres confusion
        new McpTestCase
        {
            IssueNumber = 29,
            Title = "Postgres tool rejects connection string",
            Body = "Error: Unsupported authentication method.\nUser mistakenly provided SQL connection string format.",
            ExpectedServerLabel = "server-Azure.Mcp",
            ExpectedToolLabel = "tools-Postgres",
            Notes = "SQL/PSQL confusion test"
        },

        // 30 — TRICKY: Proxy issue misclassified as Auth
        new McpTestCase
        {
            IssueNumber = 30,
            Title = "Behind proxy, storage upload fails with ETIMEDOUT",
            Body = "Even though error looks like an auth issue, root cause is network proxy.\nNo auth failure indicators.",
            ExpectedServerLabel = "server-Azure.Mcp",
            ExpectedToolLabel = "tools-Core",
            Notes = "Network vs Auth confusion"
        }
            };
        }

        /// <summary>
        /// Get a subset of test cases for quick validation
        /// </summary>
        public static List<McpTestCase> GetSmokeTestCases()
        {
            var all = GetTestCases();
            return new List<McpTestCase>
            {
                all[0],  // Blob Storage
                all[1],  // Key Vault
                all[4],  // General server issue
                all[11]  // Feature request
            };
        }

        /// <summary>
        /// Save test cases to JSON file for external tooling
        /// </summary>
        public static void ExportToJson(string filePath)
        {
            var testCases = GetTestCases();
            var json = JsonSerializer.Serialize(testCases, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(filePath, json);
        }
    }
}
