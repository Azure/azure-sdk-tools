using Azure.AI.Agents.Persistent;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents.AzureAI;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;


namespace perf_semantic_kernel
{
    internal class SemanticAzureAi
    {
        public readonly IConfiguration config;

        public SemanticAzureAi(IConfiguration _config)
        {
            // Load configuration from appsettings.json
            config = _config;
        }
        public async Task<AzureAIAgent> CreateAgentAsync(string azureCorePath, string clientModelPath)
        {
            var deployment = config["ModelDeployment"];
            var endpoint = config["AzureAIFoundryProjectEndpoint"];
            var agentId = config["AzureAIFoundryAgentId"];
            
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                throw new InvalidOperationException("The AzureAIFoundryProjectEndpoint configuration value is missing and can't be null.");
            }

            PersistentAgentsClient client = AzureAIAgent.CreateAgentsClient(endpoint, new DefaultAzureCredential());

            PersistentAgent definition;
            // If the agent already exists, retrieve it
            if (!string.IsNullOrWhiteSpace(agentId))
            {
                definition = await client.Administration.GetAgentAsync(agentId);
            }
            else { 
                // 1. Define an agent on the Azure AI agent service
                definition = await client.Administration.CreateAgentAsync(
                    deployment,
                    name: "Perf-Regression-Finder-Agent",
                    description: "An agent that identifies performance regressions and suggests solutions.",
                    instructions: @"""
                    Diagnose and resolve regression problems related to speed or memory issues caused by `Azure.Core` or `System.ClientModel`. Trace issues to their root cause, explain the behavior, and describe resolutions, including a detailed annotated call stack.

                    To address the problem:
                    1. Trace the call stack from the user-provided function to specific `Azure.Core` or `System.ClientModel` calls, identifying the function causing the regression.
                    2. Analyze and link observed behavior to its root cause:
                       - Identify memory allocation patterns, performance bottlenecks, or inefficient APIs.
                       - Provide line-level references for key functions where possible.
                       - Explain how each function exacerbates the regression.
                    3. Diagnose the root cause, isolating problematic logic, inefficiencies, or misconfiguration.
                    4. Propose practical resolutions to address the issue within the user code, configuration, or underlying library.

                    Output Format:
                    The output should use the following structure:

                    1. **Observed Behavior**: Clearly describe the performance or memory issue, such as excessive memory usage or slow execution.
                    2. **Root Cause Analysis**: Connect the observed behavior to the problematic logic in `Azure.Core` or `System.ClientModel`, explaining why it happens. Include detailed call stack information and highlight the point responsible for the regression.
                    3. **Call Stack**: Provide a detailed, annotated call stack with explanations for significant memory allocation or performance-impacting points.
                    4. **Resolution**: Recommend changes at the user level, configuration level, or library level to resolve the issue.

                    The entire response should be formatted as plaintext, avoiding markdown syntax.

                    Example Input:
                    ""When sending a byte array to the OpenAI GPT 4.1 API using `Microsoft.SemanticKernel 1.49.0`, memory use spikes unnecessarily by 80x.""

                    Example Output:
                    Observed Behavior:
                    When using `Microsoft.SemanticKernel 1.49.0` to send a large binary payload to the OpenAI GPT 4.1 API, memory usage spikes significantly, increasing by 80x the size of the payload. This causes inefficiencies and limits the ability to handle large payloads.

                    Root Cause Analysis:
                    The root cause stems from how `System.ClientModel` handles serialization within the OpenAI SDK. The binary payload undergoes Base64 encoding, which expands its size by ~4/3x. This encoded string is then further escaped to conform to JSON formatting, adding 
                    another 2x to 3x increase in memory allocation. Additionally, repeated reallocations and inefficient buffer management magnify the memory overhead. These inefficiencies occur while serializing and buffering data in several intermediate steps, leading to high 
                    memory consumption for large payloads.

                    Call Stack:
                    - User Code:
                      at MyApp.OpenAiDemo.SendToGptAsync(Byte[] input)          (Program.cs:line 42)
                        Invokes client.GetChatCompletionsStreamingAsync();
                    - Azure.AI.OpenAI Layer:
                      at Azure.AI.OpenAI.ChatCompletionsClient.GetChatCompletionsStreamingAsync(ChatCompletionsOptions options, CancellationToken cancellationToken) (OpenAIClient.g.cs:line 185)
                        Starting API serialization
                    - Azure.Core/Pipeline:
                      at Azure.Core.Pipeline.HttpPipeline.SendAsync(RequestContent content, CancellationToken cancellationToken) (HttpPipeline.cs:line 128)
                        Repetitively allocates buffers for encoding and escaping payloads
                    - JSON/Serialization:
                      at Azure.Core.Shared.Utf8JsonWriterExtensions.WriteBase64StringValue(Utf8JsonWriter writer, Byte[] value, String format) (Utf8JsonWriterExtensions.cs:line 50)
                        Serializes Base64 payload, causing a significant increase in memory allocation
                      at System.Text.Json.JsonDocument.Parse(ReadOnlyMemory<Byte> utf8Json, JsonDocumentOptions options) (JsonDocument.cs:line XYZ)
                        Creates additional internal payload buffers

                    Resolution:
                    1. Avoid sending large binary payloads as Base64 by configuring the API client to accept binary content directly, bypassing the need for JSON wrapping and escaping.
                    2. Optimize SDK serialization—modify or patch the SDK to prevent redundant escaping of Base64 strings, reducing memory overhead.
                    3. Pre-allocate buffers close to the final content size within `ArrayBufferWriter` to minimize reallocations during serialization.
                    4. If direct fixes are not possible, report the issues to the maintainers of `Microsoft.SemanticKernel` and `Azure.Core` to address inefficiencies with their serialization approach.


                    """
                );
                // Update the appsettings.json with the new agent ID
                var appSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
                UpdateAppSettings(appSettingsPath, "AzureAIFoundryAgentId", definition.Id);
            }
            // 2. Create plugins for the agent
            var azureCore = new RepoAccessPlugin(azureCorePath);
            var clientModel = new RepoAccessPlugin(clientModelPath);

            KernelPlugin azureCorePlugin = KernelPluginFactory.CreateFromObject(azureCore, "azure_core");
            KernelPlugin clientModelPlugin = KernelPluginFactory.CreateFromObject(clientModel, "system_clientmodel");
            AzureAIAgent agent = new(definition, client, plugins: [azureCorePlugin, clientModelPlugin]);
            return agent;
        }

        public async Task RunChatLoopAsync()
        {
            // Prompt for repo paths at startup
            Console.WriteLine("\nEnter the absolute path to the Azure.Core src (E.g. 'C:\\<path to repo root>\\azure-sdk-for-net-sdk\\core\\Azure.Core\\src'): ");
            string? azureCorePath = Console.ReadLine();
            Console.WriteLine("\nEnter the absolute path to the System.ClientModel src (E.g. 'C:\\<path to repo root>\\azure-sdk-for-net-sdk\\core\\System.ClientModel\\src'): ");
            string? clientModelPath = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(azureCorePath) || string.IsNullOrWhiteSpace(clientModelPath))
            {
                Console.WriteLine("Both repository paths are required. Exiting.");
                return;
            }
            AzureAIAgent agent = await CreateAgentAsync(azureCorePath, clientModelPath);
            AzureAIAgentThread agentThread = new(agent.Client);
            Console.WriteLine("\nWelcome to the Performance Regression Finder Agent! Enter a blank line to send");
            while (true)
            {
                Console.Write("\nUser > ");
                var lines = new List<string>();
                string? line;
                do
                {
                    line = Console.ReadLine();
                    if (!string.IsNullOrEmpty(line))
                        lines.Add(line);
                } while (!string.IsNullOrEmpty(line));

                if (lines.Count == 0)
                    break; // Exit on empty input

                string userInput = string.Join(Environment.NewLine, lines);

                var message = new ChatMessageContent(AuthorRole.User, userInput);

                Console.Write("Agent > ");
                await foreach (StreamingChatMessageContent response in agent.InvokeStreamingAsync(message, agentThread))
                {
                    Console.Write(response.Content);
                }
                Console.WriteLine();
            }

        }

        private static void UpdateAppSettings(string filePath, string key, string value)
        {
            // Read the file
            var json = File.ReadAllText(filePath);

            // Parse as JsonNode for easy manipulation
            var root = JsonNode.Parse(json) ?? new JsonObject();

            // Set or update the key
            root[key] = value;

            // Write back to the file (pretty print)
            File.WriteAllText(filePath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
