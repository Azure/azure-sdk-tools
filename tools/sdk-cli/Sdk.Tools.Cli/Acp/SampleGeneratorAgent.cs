using System.Runtime.CompilerServices;
using System.Text.Json;
using AgentClientProtocol.Sdk;
using AgentClientProtocol.Sdk.Schema;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sdk.Tools.Cli.Helpers;
using Sdk.Tools.Cli.Models;
using Sdk.Tools.Cli.Services;
using Sdk.Tools.Cli.Services.Languages;
using Sdk.Tools.Cli.Services.Languages.Samples;

namespace Sdk.Tools.Cli.Acp;

/// <summary>
/// ACP agent implementation for interactive sample generation.
/// Uses the AgentClientProtocol.Sdk for protocol handling.
/// </summary>
public class SampleGeneratorAgent : IAgent
{
    private readonly IServiceProvider _services;
    private readonly ILogger<SampleGeneratorAgent> _logger;
    
    // Agent sessions keyed by session ID
    private readonly Dictionary<string, AgentSessionState> _sessions = new();
    
    // Store connection per session for interactive generation
    private AgentSideConnection? _currentConnection;
    
    public SampleGeneratorAgent(IServiceProvider services, ILogger<SampleGeneratorAgent> logger)
    {
        _services = services;
        _logger = logger;
    }
    
    public void SetConnection(AgentSideConnection connection)
    {
        _currentConnection = connection;
    }
    
    public Task<InitializeResponse> InitializeAsync(InitializeRequest request, CancellationToken ct = default)
    {
        return Task.FromResult(new InitializeResponse
        {
            ProtocolVersion = Protocol.Version,
            AgentCapabilities = new AgentCapabilities
            {
                SessionCapabilities = new SessionCapabilities()
            },
            AgentInfo = new Implementation
            {
                Name = "sdk-cli",
                Version = "1.0.0",
                Title = "SDK CLI Sample Generator"
            }
        });
    }
    
    public Task<NewSessionResponse> NewSessionAsync(NewSessionRequest request, CancellationToken ct = default)
    {
        var sessionId = $"sess_{Guid.NewGuid():N}";
        
        var state = new AgentSessionState
        {
            SessionId = sessionId,
            WorkingDirectory = request.Cwd  // Store the cwd from the session
        };
        
        _sessions[sessionId] = state;
        _logger.LogDebug("Created session {SessionId} with cwd {Cwd}", sessionId, request.Cwd);
        
        return Task.FromResult(new NewSessionResponse
        {
            SessionId = sessionId
        });
    }
    
    public async Task<PromptResponse> PromptAsync(PromptRequest request, CancellationToken ct = default)
    {
        if (!_sessions.TryGetValue(request.SessionId, out var sessionState))
        {
            throw new InvalidOperationException($"Unknown session: {request.SessionId}");
        }
        
        // Use workspace path from session's cwd
        var workspacePath = sessionState.WorkingDirectory ?? ".";
        
        // Stream status to client
        if (_currentConnection != null)
        {
            await _currentConnection.SendTextAsync(request.SessionId, $"Analyzing SDK at {workspacePath}...\n", ct);
        }
        
        // Get services
        var aiService = _services.GetRequiredService<AiService>();
        var fileHelper = _services.GetRequiredService<FileHelper>();
        
        // Auto-detect source, samples folders, and language
        var sdkInfo = SdkInfo.Scan(workspacePath);
        if (sdkInfo.Language == null)
        {
            if (_currentConnection != null)
            {
                await _currentConnection.SendTextAsync(request.SessionId, "Could not detect SDK language.\n", ct);
            }
            return new PromptResponse { StopReason = StopReason.EndTurn };
        }
        
        if (_currentConnection != null)
        {
            await _currentConnection.SendTextAsync(request.SessionId, $"Detected {sdkInfo.LanguageName} SDK\n", ct);
        }
        
        if (_currentConnection != null)
        {
            await _currentConnection.SendTextAsync(request.SessionId, $"Source: {sdkInfo.SourceFolder}\n", ct);
            await _currentConnection.SendTextAsync(request.SessionId, $"Output: {sdkInfo.SuggestedSamplesFolder}\n\n", ct);
            await _currentConnection.SendTextAsync(request.SessionId, "Generating samples...\n", ct);
        }
        
        // Create appropriate language context
        var context = CreateLanguageContext(sdkInfo.Language.Value, fileHelper);
        
        var systemPrompt = $"Generate runnable SDK samples. {context.GetInstructions()}";
        var userPromptStream = StreamUserPromptAsync(sdkInfo.SourceFolder, context, ct);
        
        // Stream parsed samples as they complete
        List<GeneratedSample> samples = [];
        await foreach (var sample in aiService.StreamItemsAsync<GeneratedSample>(
            systemPrompt, userPromptStream, null, null, ct))
        {
            if (!string.IsNullOrEmpty(sample.Name) && !string.IsNullOrEmpty(sample.Code))
            {
                samples.Add(sample);
            }
        }
        
        var outputFolder = sdkInfo.SuggestedSamplesFolder;
        Directory.CreateDirectory(outputFolder);
        
        foreach (var sample in samples)
        {
            var filename = SanitizeFileName(sample.Name) + context.FileExtension;
            var filePath = Path.Combine(outputFolder, filename);
            await File.WriteAllTextAsync(filePath, sample.Code, ct);
            
            if (_currentConnection != null)
            {
                await _currentConnection.SendTextAsync(request.SessionId, $"✓ {filename}\n", ct);
            }
        }
        
        _logger.LogInformation("Generated {Count} samples in {Path}", samples.Count, outputFolder);
        
        if (_currentConnection != null)
        {
            await _currentConnection.SendTextAsync(request.SessionId, $"\nDone! Generated {samples.Count} sample(s)\n", ct);
        }
        
        return new PromptResponse { StopReason = StopReason.EndTurn };
    }
    
    private SampleLanguageContext CreateLanguageContext(SdkLanguage language, FileHelper fileHelper) => language switch
    {
        SdkLanguage.DotNet => new DotNetSampleLanguageContext(fileHelper),
        SdkLanguage.Python => new PythonSampleLanguageContext(fileHelper),
        SdkLanguage.JavaScript => new JavaScriptSampleLanguageContext(fileHelper),
        SdkLanguage.TypeScript => new TypeScriptSampleLanguageContext(fileHelper),
        SdkLanguage.Java => new JavaSampleLanguageContext(fileHelper),
        SdkLanguage.Go => new GoSampleLanguageContext(fileHelper),
        _ => new DotNetSampleLanguageContext(fileHelper) // fallback
    };
    
    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new System.Text.StringBuilder(name);
        foreach (var c in invalid) sanitized.Replace(c, '_');
        sanitized.Replace(':', '_');
        sanitized.Replace(' ', '_');
        return sanitized.ToString();
    }
    
    public Task CancelAsync(CancelNotification notification, CancellationToken ct = default)
    {
        _logger.LogDebug("Cancel requested for session {SessionId}", notification.SessionId);
        return Task.CompletedTask;
    }
    
    private static async IAsyncEnumerable<string> StreamUserPromptAsync(
        string sourceFolder,
        SampleLanguageContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return "Generate samples for this SDK:\n";
        
        await foreach (var chunk in context.StreamContextAsync(
            new[] { sourceFolder }, null, ct: cancellationToken))
        {
            yield return chunk;
        }
    }
    
    /// <summary>Internal state for a session.</summary>
    private class AgentSessionState
    {
        public required string SessionId { get; init; }
        public string? WorkingDirectory { get; init; }
    }
}
