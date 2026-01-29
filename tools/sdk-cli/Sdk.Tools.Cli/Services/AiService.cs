// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.ClientModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAI;
using OpenAI.Chat;
using Sdk.Tools.Cli.Models;

namespace Sdk.Tools.Cli.Services;

/// <summary>
/// Unified AI service that supports both GitHub Copilot SDK and OpenAI-compatible APIs.
/// 
/// Environment variables:
/// - SDK_CLI_USE_OPENAI: Set to "true" to use OpenAI-compatible API
/// - OPENAI_ENDPOINT: Custom endpoint URL (defaults to https://api.openai.com/v1)
/// - OPENAI_API_KEY: API key for the endpoint
/// - SDK_CLI_MODEL: Override the default model
/// - SDK_CLI_DEBUG: Set to "true" to enable debug logging
/// - SDK_CLI_DEBUG_DIR: Directory for debug log files
/// - SDK_CLI_TIMEOUT: Request timeout in seconds (default: 300)
/// </summary>
public class AiService : IAsyncDisposable
{
    private readonly ILogger<AiService> _logger;
    private readonly AiProviderSettings _settings;
    private readonly AiDebugLogger _debugLogger;
    private readonly SemaphoreSlim _clientLock = new(1, 1);
    private readonly TimeSpan _requestTimeout;
    
    // Copilot SDK client (lazy initialized)
    private CopilotClient? _copilotClient;
    
    // OpenAI client (lazy initialized)
    private ChatClient? _openAiClient;
    
    public AiService(ILogger<AiService> logger, AiProviderSettings? settings = null, AiDebugLogger? debugLogger = null)
    {
        _logger = logger;
        _settings = settings ?? AiProviderSettings.FromEnvironment();
        _debugLogger = debugLogger ?? new AiDebugLogger(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AiDebugLogger>.Instance, 
            _settings);
        
        // Parse timeout from environment (default 5 minutes)
        var timeoutStr = Environment.GetEnvironmentVariable("SDK_CLI_TIMEOUT");
        _requestTimeout = int.TryParse(timeoutStr, out var timeoutSec) 
            ? TimeSpan.FromSeconds(timeoutSec) 
            : TimeSpan.FromMinutes(5);
    }
    
    /// <summary>
    /// Whether using OpenAI-compatible API instead of Copilot.
    /// </summary>
    public bool IsUsingOpenAi => _settings.UseOpenAi;
    
    /// <summary>
    /// Stream AI response and yield parsed items as they complete.
    /// Automatically appends the JSON schema for type T to the system prompt.
    /// Each complete JSON object is deserialized and yielded as it streams in.
    /// </summary>
    public async IAsyncEnumerable<T> StreamItemsAsync<T>(
        string systemPrompt,
        IAsyncEnumerable<string> userPromptStream,
        string? model = null,
        ContextInfo? contextInfo = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Materialize the streamed prompt (APIs require full prompt upfront)
        var promptBuilder = new StringBuilder();
        await foreach (var chunk in userPromptStream.WithCancellation(cancellationToken))
        {
            promptBuilder.Append(chunk);
        }
        var userPrompt = promptBuilder.ToString();
        
        // Append type schema to system prompt for structured output
        var schema = GenerateJsonSchema<T>();
        var enhancedSystemPrompt = $"{systemPrompt}\n\nRespond with a JSON array of objects matching this schema:\n{schema}";
        
        var effectiveModel = _settings.GetModel(model);
        var provider = _settings.UseOpenAi ? "OpenAI" : "Copilot";
        
        _logger.LogDebug("Streaming AI with model {Model} (provider: {Provider})", 
            effectiveModel, provider);
        
        // Start debug session
        var debugSession = _debugLogger.StartSession(
            provider,
            effectiveModel,
            _settings.Endpoint,
            enhancedSystemPrompt,
            userPrompt,
            contextInfo);
        
        var responseBuilder = new StringBuilder();
        var buffer = new StringBuilder();
        var inJsonObject = false;
        var braceDepth = 0;
        
        IAsyncEnumerable<string> stream = _settings.UseOpenAi
            ? StreamOpenAiAsync(enhancedSystemPrompt, userPrompt, effectiveModel, cancellationToken)
            : StreamCopilotAsync(enhancedSystemPrompt, userPrompt, effectiveModel, cancellationToken);
        
        await using var enumerator = stream.GetAsyncEnumerator(cancellationToken);
        
        while (true)
        {
            string chunk;
            try
            {
                if (!await enumerator.MoveNextAsync())
                    break;
                chunk = enumerator.Current;
            }
            catch (Exception ex)
            {
                await _debugLogger.CompleteSessionAsync(debugSession, responseBuilder.ToString(), streaming: true, error: ex);
                throw;
            }
            
            responseBuilder.Append(chunk);
            
            // Parse JSON objects as they complete
            foreach (var c in chunk)
            {
                buffer.Append(c);
                
                if (c == '{')
                {
                    if (!inJsonObject) inJsonObject = true;
                    braceDepth++;
                }
                else if (c == '}')
                {
                    braceDepth--;
                    
                    if (inJsonObject && braceDepth == 0)
                    {
                        var jsonStr = ExtractLastJsonObject(buffer.ToString());
                        if (jsonStr != null)
                        {
                            var item = TryDeserialize<T>(jsonStr);
                            if (item != null)
                            {
                                yield return item;
                            }
                        }
                        
                        buffer.Clear();
                        inJsonObject = false;
                    }
                }
            }
        }
        
        await _debugLogger.CompleteSessionAsync(debugSession, responseBuilder.ToString(), streaming: true);
    }
    
    private static string GenerateJsonSchema<T>()
    {
        var type = typeof(T);
        var properties = type.GetProperties()
            .Where(p => p.CanRead)
            .Select(p => $"  \"{ToCamelCase(p.Name)}\": {GetJsonType(p.PropertyType)}")
            .ToList();
        
        return "{\n" + string.Join(",\n", properties) + "\n}";
    }
    
    private static string ToCamelCase(string name) =>
        string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];
    
    private static string GetJsonType(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        
        if (underlying == typeof(string)) return "\"string\"";
        if (underlying == typeof(int) || underlying == typeof(long) || underlying == typeof(double) || underlying == typeof(float) || underlying == typeof(decimal)) return "number";
        if (underlying == typeof(bool)) return "boolean";
        if (underlying.IsArray || (underlying.IsGenericType && underlying.GetGenericTypeDefinition() == typeof(List<>))) return "array";
        
        return "\"string\""; // Default fallback
    }
    
    private static string? ExtractLastJsonObject(string text)
    {
        var lastBrace = text.LastIndexOf('}');
        if (lastBrace == -1) return null;
        
        // Find matching opening brace
        var depth = 0;
        for (var i = lastBrace; i >= 0; i--)
        {
            if (text[i] == '}') depth++;
            else if (text[i] == '{') depth--;
            
            if (depth == 0)
            {
                return text.Substring(i, lastBrace - i + 1);
            }
        }
        return null;
    }
    
    private static T? TryDeserialize<T>(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });
        }
        catch
        {
            return default;
        }
    }
    
    #region OpenAI Implementation
    
    private ChatClient GetOpenAiClient(string model)
    {
        if (_openAiClient is not null) return _openAiClient;
        
        var apiKey = _settings.ApiKey;
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException(
                "OPENAI_API_KEY environment variable is required when using OpenAI mode. " +
                "Set SDK_CLI_USE_OPENAI=true and OPENAI_API_KEY=your-key");
        }
        
        var endpoint = _settings.GetEndpoint();
        var credential = new ApiKeyCredential(apiKey);
        
        OpenAIClient client;
        if (endpoint != null)
        {
            _logger.LogDebug("Creating OpenAI client for custom endpoint {Endpoint}", endpoint);
            var options = new OpenAIClientOptions { Endpoint = endpoint };
            client = new OpenAIClient(credential, options);
        }
        else
        {
            _logger.LogDebug("Creating OpenAI client for default endpoint (api.openai.com)");
            client = new OpenAIClient(credential);
        }
        
        _openAiClient = client.GetChatClient(model);
        return _openAiClient;
    }
    
    private async IAsyncEnumerable<string> StreamOpenAiAsync(
        string systemPrompt,
        string userPrompt,
        string model,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var client = GetOpenAiClient(model);
        
        List<ChatMessage> messages = [new SystemChatMessage(systemPrompt), new UserChatMessage(userPrompt)];
        
        await foreach (var update in client.CompleteChatStreamingAsync(messages, cancellationToken: cancellationToken))
        {
            foreach (var part in update.ContentUpdate)
            {
                if (!string.IsNullOrEmpty(part.Text))
                {
                    yield return part.Text;
                }
            }
        }
    }
    
    #endregion
    
    #region Copilot Implementation
    
    private async Task<CopilotClient> GetCopilotClientAsync(CancellationToken cancellationToken)
    {
        if (_copilotClient is not null) return _copilotClient;
        
        await _clientLock.WaitAsync(cancellationToken);
        try
        {
            if (_copilotClient is not null) return _copilotClient;
            
            _logger.LogDebug("Initializing GitHub Copilot client...");
            
            var cliPath = Environment.GetEnvironmentVariable("COPILOT_CLI_PATH") ?? "copilot";
            _logger.LogDebug("Using Copilot CLI at: {CliPath}", cliPath);
            
            _copilotClient = new CopilotClient(new CopilotClientOptions
            {
                CliPath = cliPath,
                UseStdio = true,
                AutoStart = true,
                LogLevel = "debug"
            });
            
            await _copilotClient.StartAsync();
            _logger.LogDebug("GitHub Copilot client started successfully");
            
            return _copilotClient;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start GitHub Copilot client");
            throw new InvalidOperationException(
                $"Failed to start GitHub Copilot client: {ex.Message}. " +
                "Ensure the Copilot CLI is installed and authenticated, " +
                "or use --use-openai flag with SDK_CLI_OPENAI_API_KEY set.", ex);
        }
        finally
        {
            _clientLock.Release();
        }
    }
    
    private async IAsyncEnumerable<string> StreamCopilotAsync(
        string systemPrompt,
        string userPrompt,
        string model,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var client = await GetCopilotClientAsync(cancellationToken);
        
        await using var session = await client.CreateSessionAsync(new SessionConfig
        {
            Model = model,
            Streaming = true,
            SystemMessage = new SystemMessageConfig
            {
                Mode = SystemMessageMode.Append,
                Content = systemPrompt
            },
            AvailableTools = new List<string>()
        });
        
        var chunks = System.Threading.Channels.Channel.CreateUnbounded<string>();
        
        using var subscription = session.On(evt =>
        {
            switch (evt)
            {
                case AssistantMessageDeltaEvent delta:
                    chunks.Writer.TryWrite(delta.Data.DeltaContent ?? "");
                    break;
                case SessionIdleEvent:
                    chunks.Writer.Complete();
                    break;
                case SessionErrorEvent err:
                    chunks.Writer.Complete(new InvalidOperationException($"Session error: {err.Data.Message}"));
                    break;
            }
        });
        
        await session.SendAsync(new MessageOptions { Prompt = userPrompt });
        
        await foreach (var chunk in chunks.Reader.ReadAllAsync(cancellationToken))
        {
            yield return chunk;
        }
    }
    
    #endregion
    
    public async ValueTask DisposeAsync()
    {
        if (_copilotClient != null)
        {
            await _copilotClient.StopAsync();
            await _copilotClient.DisposeAsync();
        }
        _clientLock.Dispose();
    }
}
