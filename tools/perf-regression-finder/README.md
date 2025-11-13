# Perf-Regression-Finder Agent
## Overview

A C#/.NET 8 project for diagnosing and solving performance regressions in Azure SDKs using Semantic Kernel and Azure AI Agents.
It provides the call-stack and suggestions of where a regression could be occurring. It works with using the plugin for access to the `Azure.Core` and `System.ClientModel` and can be extended to have more functionality
with an addition of more plugins.

## Features

- Diagnoses speed and memory regressions in `Azure.Core` and `System.ClientModel`
- Traces issues to root causes with annotated call stacks
- Provides plain text output
- Supports interactive, multi-line user input

## Getting Started

1. **Clone the repository:**
```
> git clone https://github.com/Azure/azure-sdk-tools.git
```
*Note: This is required for the agent to work*

2. **Configure your environment:**
- Update `appsettings.json` with your Azure OpenAI and agent details. This can be found in the openai-shared resource group, under the perf-regression-finder-agent.
**This should be placed in the ./artifacts\bin\perf-regression-finder\Debug\net8.0 folder*

3. **Build and run:**
- Navigate to the `perf-regression-finder/src` directory and run:
```
> dotnet run
```
*Note: The agent uses the 'Microsoft.SemanticKernel.Agents.AzureAI.AzureAIAgent' package which is in preview and subject to change. The 'Microsoft.SemanticKernel.ChatCompletion' can also be used alternatively.*

## Usage

- Start the application and follow the prompts.
- Enter the paths for Azure.Core and System.ClientModel source code.
- Enter your performance regression scenario or question.
- The agent will analyze and provide a detailed, structured response.

## Project Structure

- `SemanticAzureAi.cs` - Main agent logic and chat loop
- `RepoAccessPlugin.cs` - Plugin for accessing Azure SDK source code
- `Program.cs` - Entry point

## Requirements

- .NET 8 SDK
- Access to Azure OpenAI and Persistent Agents
- Local clone of Azure SDK repositories (for plugin access)

## Walkthrough
1. Run `dotnet run` in the `perf-regression-finder` directory.
```
> dotnet run
Welcome to the Performance Regression Finder Agent! Enter a blank line to send
```
2. Enter your performance regression scenario or question:
```
User > when sending a byte array to 'GetMessageContentAsync' specifically targeting OpenAi Gpt 4.1, I can see the memory usage spike by 80x the original byte arrays size. 
propose a root cause by searching through the Azure.Core and System.Clientmodel source code as well as a call stack
```

3. The agent will analyze the input and provide a structured response:
```
Agent > Observed Behavior:
When sending a byte array to the GetMessageContentAsync method targeting OpenAI GPT 4.1, memory usage spikes disproportionately high, approximately 80 times the original byte array size. This excessive allocation results in large memory consumption and performance degradation.

Root Cause Analysis:
The root cause is likely in how Azure.Core serializes and sends byte arrays in HTTP requests. From the investigation of Azure.Core code, particularly the RequestContent class, when byte arrays are transformed into RequestContent via Create(byte[]), Azure.Core wraps this in an ArrayContent class. During request-sending, this content is copied and serialized multiple times.

Notably, in RequestContent.MemoryContent and RequestContent.ReadOnlySequenceContent implementations, the WriteTo and WriteToAsync methods invoke ToArray() on buffers, which copies the entire data buffer into new arrays before writing them to the outgoing Stream. This leads to memory duplication equal to or greater than the input size.

Additionally, Azure.Core’s HttpClientTransportTransportRequest class wraps RequestContent into PipelineContentAdapter, which serializes this content asynchronously by calling WriteToAsync multiple times for retries or streaming purposes, potentially doubling or more the allocations temporarily.

If higher-level SDK (e.g. OpenAI client) serializes the byte array further by Base64 encoding in JSON before wrapping it into RequestContent, this exacerbates the memory overhead, as Base64 increases the data size by ~33%, and JSON escaping inflates it further.

Call Stack:
- User Code:
  at UserNamespace.OpenAI.SendByteArrayAsync(byte[] data) (UserCode.cs:line N)
    Invokes AzureOpenAIClient.GetMessageContentAsync(...)
- OpenAI SDK Layer:
  at Azure.OpenAI.Client.GetMessageContentAsync(...)
    Serializes request content, potentially base64-encoding the byte array.
- Azure.Core:
  at Azure.Core.RequestContent.Create(byte[] bytes) (RequestContent.cs:line 42)
    Wraps byte array into ArrayContent.
  at Azure.Core.RequestContent.ArrayContent.WriteToAsync(Stream stream, CancellationToken cancellation) (RequestContent.cs:line 284)
    Writes byte[] buffer to stream.
  at Azure.Core.Pipeline.HttpClientTransport.HttpClientTransportRequest.PipelineContentAdapter.SerializeToStreamAsync(Stream stream, TransportContext? context) (HttpClientTransport.Request.cs:line 293)
    Calls WriteToAsync of underlying RequestContent.
  at Azure.Core.Pipeline.HttpPipeline.SendAsync(HttpMessage message, CancellationToken cancellationToken) (HttpPipeline.cs:line 132)
    Sends HTTP request pipeline, includes retries and buffering.

Resolution:
1. Avoid serializing the byte array as Base64 JSON string by using binary HTTP content if API supports it, to prevent payload size expansion.
2. Use Azure.Core's memory-efficient RequestContent.Create(ReadOnlyMemory<byte>) overloads where possible.
3. Minimize copies by avoiding calls to ToArray() on buffers before streaming; patch or extend SDK if needed to improve streaming without buffering.
4. In client code, if sending large blobs, stream data via StreamContent instead of full byte arrays to reduce memory spikes.
5. Contact maintainers to optimize WriteToAsync implementations to support zero-copy streaming or pooling buffers.
6. Profile memory usage to identify redundant buffering or duplicate copies during serialization in user code or SDK.

This approach should significantly reduce the peak memory usage by preventing multiple large buffer allocations and avoiding encoding-induced size inflation.
```
4. You can continue to interact with the agent by entering more scenarios or questions:
```
User > can you do a more detailed call stack?
```
5. The agent will provide a more detailed call stack and analysis:
```
Agent > Detailed Call Stack with Annotations:

User Code:
  at MyApp.OpenAiExample.SendByteArrayAsync(Byte[] data) (OpenAiExample.cs:line 120)
    - The user's method sends a byte array payload to the OpenAI GPT 4.1 API using the SDK.
    - Calls client method (e.g. GetMessageContentAsync) passing the byte array.

OpenAI SDK Layer:
  at Azure.AI.OpenAI.OpenAIClient.GetMessageContentAsync(RequestContent content, CancellationToken cancellationToken) (OpenAIClient.g.cs:line 185)
    - Converts user payload into RequestContent for the HTTP request.
    - Likely base64-encodes the byte array in JSON, increasing payload size 1.33x or more.

Azure.Core – RequestContent:
  at Azure.Core.RequestContent.Create(byte[] bytes) (RequestContent.cs:line 42)
    - Wraps the byte array into an ArrayContent instance.
    - This triggers the use of ArrayContent.WriteToAsync for streaming content.

Azure.Core – RequestContent.ArrayContent.WriteToAsync:
  at Azure.Core.RequestContent.ArrayContent.WriteToAsync(Stream stream, CancellationToken cancellationToken) (RequestContent.cs:line 284)
    - Writes the internal byte[] data (_bytes) from the content to HTTP request stream.
    - Uses stream.WriteAsync(_bytes, index, length) which allocates buffers internally.
    - This invocation can be repeated for retries or streaming scenarios causing temporary doubling of buffer memory.

Azure.Core – HttpClientTransport.HttpClientTransportRequest.PipelineContentAdapter.SerializeToStreamAsync:
  at Azure.Core.Pipeline.HttpClientTransport.HttpClientTransportRequest.PipelineContentAdapter.SerializeToStreamAsync(Stream stream, TransportContext? context) (HttpClientTransport.Request.cs:line 293)
    - Adapts Azure.Core RequestContent to HttpContent for HttpClient consumption.
    - Invokes the RequestContent's WriteToAsync to serialize content to stream.

Azure.Core – HttpPipeline.SendAsync:
  at Azure.Core.Pipeline.HttpPipeline.SendAsync(HttpMessage message, CancellationToken cancellationToken) (HttpPipeline.cs:line 132)
    - Sends the HTTP message asynchronously through the pipeline.
    - This includes pipeline policies and transport sending.
    - May perform retries causing multiple Serializations.

Azure.Core – ClientPipeline.SendAsync:
  at System.ClientModel.Primitives.ClientPipeline.SendAsync(PipelineMessage message) (ClientPipeline.cs:line 233)
    - Starts the pipeline message processing asynchronously.
    - Invokes the first pipeline policy's async Process.

Azure.Core – PipelinePolicy.ProcessAsync:
  at Azure.Core.Pipeline.PipelinePolicy.ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int index) (PipelinePolicy.cs:line 52)
    - Processes the message through policies one-by-one.
    - Eventually calls the transport policy causing the actual HTTP send.

Azure.Core – RetryPolicy:
  at Azure.Core.Pipeline.RetryPolicy.ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int index) (RetryPolicy.cs:line 120)
    - Manages retry attempts.
    - Retries cause repeated serialization of the request content, doubling memory usage during retries.

Overall cause is multiple transformations and retries invoking serialization of the byte array payload multiple times, and use of ArrayContent which copies the byte array to buffers for writing to streams.

Summary:
- User payload → base64+JSON serialization (OpenAI SDK layer)
- Wrapped into Azure.Core ArrayContent → multiple allocations during WriteToAsync
- HttpClientTransportRequest serializes content via PipelineContentAdapter
- HttpPipeline and ClientPipeline invoke SendAsync potentially multiple times if retries
- Each serialization duplicates byte array memory usage leading to large peak memory usage

This detailed call stack reveals multiple serialization points and copies increasing memory 80x for large byte arrays.
```
6. You can exit the application by entering a blank line or using Ctrl+C.