// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sdk.Tools.Cli.Models;

namespace Sdk.Tools.Cli.Services;

/// <summary>
/// Debug logger for AI service interactions.
/// Writes structured markdown files with full request/response details.
/// 
/// Enable via environment variable: SDK_CLI_DEBUG=true
/// Set output directory via: SDK_CLI_DEBUG_DIR (defaults to ~/.sdk-cli/debug)
/// </summary>
public class AiDebugLogger
{
    private readonly ILogger<AiDebugLogger> _logger;
    private readonly AiProviderSettings _settings;
    private readonly string _debugDir;
    private readonly bool _enabled;
    
    public AiDebugLogger(ILogger<AiDebugLogger> logger, AiProviderSettings settings)
    {
        _logger = logger;
        _settings = settings;
        _enabled = settings.DebugEnabled;
        _debugDir = settings.DebugDirectory ?? GetDefaultDebugDir();
        
        if (_enabled)
        {
            Directory.CreateDirectory(_debugDir);
            _logger.LogInformation("AI Debug logging enabled. Logs will be written to: {DebugDir}", _debugDir);
        }
    }
    
    private static string GetDefaultDebugDir()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".sdk-cli", "debug");
    }
    
    /// <summary>
    /// Log an AI request before it's sent.
    /// </summary>
    public AiDebugSession StartSession(
        string provider,
        string model,
        string? endpoint,
        string systemPrompt,
        string userPrompt,
        ContextInfo? contextInfo = null)
    {
        if (!_enabled) return new AiDebugSession(null, null);
        
        var session = new AiDebugSession(
            _debugDir,
            $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}"[..31]  // truncate to reasonable length
        );
        
        session.Provider = provider;
        session.Model = model;
        session.Endpoint = endpoint;
        session.SystemPrompt = systemPrompt;
        session.UserPrompt = userPrompt;
        session.ContextInfo = contextInfo;
        session.StartTime = DateTime.UtcNow;
        
        _logger.LogDebug("Started debug session {SessionId}", session.SessionId);
        
        return session;
    }
    
    /// <summary>
    /// Log the response and finalize the debug file.
    /// Streams content directly to file to handle large prompts.
    /// </summary>
    public async Task CompleteSessionAsync(
        AiDebugSession session,
        string response,
        bool streaming,
        int? promptTokens = null,
        int? completionTokens = null,
        Exception? error = null)
    {
        if (!_enabled || session.FilePath == null) return;
        
        session.Response = response;
        session.Streaming = streaming;
        session.PromptTokens = promptTokens;
        session.CompletionTokens = completionTokens;
        session.Error = error;
        session.EndTime = DateTime.UtcNow;
        
        // Stream directly to file to handle large content
        await using var writer = new StreamWriter(session.FilePath, false, Encoding.UTF8);
        await WriteMarkdownAsync(writer, session);
        
        _logger.LogDebug("Wrote debug log to {FilePath}", session.FilePath);
    }
    
    private async Task WriteMarkdownAsync(StreamWriter writer, AiDebugSession session)
    {
        // Header
        await writer.WriteLineAsync($"# AI Debug Log: {session.SessionId}");
        await writer.WriteLineAsync();
        await writer.WriteLineAsync($"**Generated:** {session.StartTime:yyyy-MM-dd HH:mm:ss} UTC");
        await writer.WriteLineAsync();
        
        // Summary Table
        await writer.WriteLineAsync("## Summary");
        await writer.WriteLineAsync();
        await writer.WriteLineAsync("| Property | Value |");
        await writer.WriteLineAsync("|----------|-------|");
        await writer.WriteLineAsync($"| Provider | {session.Provider} |");
        await writer.WriteLineAsync($"| Model | {session.Model} |");
        await writer.WriteLineAsync($"| Endpoint | {session.Endpoint ?? "(default)"} |");
        await writer.WriteLineAsync($"| Streaming | {session.Streaming} |");
        await writer.WriteLineAsync($"| Duration | {(session.EndTime - session.StartTime)?.TotalMilliseconds:F0}ms |");
        await writer.WriteLineAsync($"| Status | {(session.Error == null ? "✅ Success" : "❌ Error")} |");
        await writer.WriteLineAsync();
        
        // Token usage if available
        if (session.PromptTokens.HasValue || session.CompletionTokens.HasValue)
        {
            await writer.WriteLineAsync("### Token Usage");
            await writer.WriteLineAsync();
            await writer.WriteLineAsync("| Type | Count |");
            await writer.WriteLineAsync("|------|-------|");
            if (session.PromptTokens.HasValue)
                await writer.WriteLineAsync($"| Prompt Tokens | {session.PromptTokens:N0} |");
            if (session.CompletionTokens.HasValue)
                await writer.WriteLineAsync($"| Completion Tokens | {session.CompletionTokens:N0} |");
            if (session.PromptTokens.HasValue && session.CompletionTokens.HasValue)
                await writer.WriteLineAsync($"| **Total** | **{session.PromptTokens + session.CompletionTokens:N0}** |");
            await writer.WriteLineAsync();
        }
        
        // Context Information
        if (session.ContextInfo != null)
        {
            await writer.WriteLineAsync("## Context Files");
            await writer.WriteLineAsync();
            
            if (session.ContextInfo.Files.Count > 0)
            {
                await writer.WriteLineAsync("| File | Size | Status |");
                await writer.WriteLineAsync("|------|------|--------|");
                
                foreach (var file in session.ContextInfo.Files.OrderByDescending(f => f.OriginalSize))
                {
                    var status = file.WasTruncated 
                        ? $"⚠️ Truncated ({file.TruncatedSize:N0}/{file.OriginalSize:N0} chars, {file.TruncationPercent:F0}%)"
                        : "✅ Full";
                    await writer.WriteLineAsync($"| `{file.RelativePath}` | {FormatBytes(file.OriginalSize)} | {status} |");
                }
                await writer.WriteLineAsync();
            }
            
            // Context stats
            await writer.WriteLineAsync("### Context Statistics");
            await writer.WriteLineAsync();
            await writer.WriteLineAsync($"- **Total Files:** {session.ContextInfo.TotalFiles}");
            await writer.WriteLineAsync($"- **Files Included:** {session.ContextInfo.FilesIncluded}");
            await writer.WriteLineAsync($"- **Files Truncated:** {session.ContextInfo.FilesTruncated}");
            await writer.WriteLineAsync($"- **Files Skipped:** {session.ContextInfo.FilesSkipped}");
            await writer.WriteLineAsync($"- **Total Context Size:** {FormatBytes(session.ContextInfo.TotalContextSize)}");
            await writer.WriteLineAsync($"- **Max Context Size:** {FormatBytes(session.ContextInfo.MaxContextSize)}");
            await writer.WriteLineAsync();
        }
        
        // System Prompt
        await writer.WriteLineAsync("## System Prompt");
        await writer.WriteLineAsync();
        await writer.WriteLineAsync("```");
        await writer.WriteLineAsync(session.SystemPrompt);
        await writer.WriteLineAsync("```");
        await writer.WriteLineAsync();
        
        // User Prompt - FULL content, no truncation
        await writer.WriteLineAsync("## User Prompt");
        await writer.WriteLineAsync();
        await writer.WriteLineAsync($"**Length:** {session.UserPrompt.Length:N0} characters (~{EstimateTokens(session.UserPrompt):N0} tokens)");
        await writer.WriteLineAsync();
        await writer.WriteLineAsync("```");
        await writer.WriteAsync(session.UserPrompt);  // Stream full content
        await writer.WriteLineAsync();
        await writer.WriteLineAsync("```");
        await writer.WriteLineAsync();
        
        // Response - FULL content, no truncation
        await writer.WriteLineAsync("## Response");
        await writer.WriteLineAsync();
        
        if (session.Error != null)
        {
            await writer.WriteLineAsync("### ❌ Error");
            await writer.WriteLineAsync();
            await writer.WriteLineAsync("```");
            await writer.WriteLineAsync(session.Error.ToString());
            await writer.WriteLineAsync("```");
        }
        else
        {
            await writer.WriteLineAsync($"**Length:** {session.Response?.Length ?? 0:N0} characters (~{EstimateTokens(session.Response ?? ""):N0} tokens)");
            await writer.WriteLineAsync();
            await writer.WriteLineAsync("```");
            await writer.WriteAsync(session.Response ?? "(empty)");  // Stream full content
            await writer.WriteLineAsync();
            await writer.WriteLineAsync("```");
        }
        await writer.WriteLineAsync();
        
        // Request Details (for debugging purposes)
        await writer.WriteLineAsync("## Request Details");
        await writer.WriteLineAsync();
        await writer.WriteLineAsync("```json");
        await writer.WriteLineAsync(JsonSerializer.Serialize(new
        {
            provider = session.Provider,
            model = session.Model,
            endpoint = session.Endpoint ?? "(default)",
            streaming = session.Streaming,
            systemPromptHash = ComputeHash(session.SystemPrompt),
            userPromptHash = ComputeHash(session.UserPrompt),
            userPromptLength = session.UserPrompt.Length,
            responseHash = ComputeHash(session.Response ?? ""),
            responseLength = session.Response?.Length ?? 0,
            durationMs = (session.EndTime - session.StartTime)?.TotalMilliseconds
        }, new JsonSerializerOptions { WriteIndented = true }));
        await writer.WriteLineAsync("```");
        await writer.WriteLineAsync();
        
        // Footer
        await writer.WriteLineAsync("---");
        await writer.WriteLineAsync($"*Debug log generated by SDK CLI v1.0.0*");
    }
    
    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }
    
    private static int EstimateTokens(string text)
    {
        // Rough estimate: ~4 characters per token
        return text.Length / 4;
    }
    
    private static string ComputeHash(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).Substring(0, 16).ToLowerInvariant();
    }
}

/// <summary>
/// Represents an active debug session for tracking request/response.
/// </summary>
public class AiDebugSession
{
    public string? SessionId { get; }
    public string? FilePath { get; }
    
    public string Provider { get; set; } = "";
    public string Model { get; set; } = "";
    public string? Endpoint { get; set; }
    public string SystemPrompt { get; set; } = "";
    public string UserPrompt { get; set; } = "";
    public string? Response { get; set; }
    public bool Streaming { get; set; }
    public ContextInfo? ContextInfo { get; set; }
    
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    
    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    public Exception? Error { get; set; }
    
    public AiDebugSession(string? debugDir, string? sessionId)
    {
        SessionId = sessionId;
        FilePath = sessionId != null && debugDir != null 
            ? Path.Combine(debugDir, $"{sessionId}.md") 
            : null;
    }
    
    public bool IsEnabled => FilePath != null;
}

/// <summary>
/// Information about files included in the context.
/// </summary>
public class ContextInfo
{
    public List<ContextFileInfo> Files { get; set; } = new();
    public int TotalFiles { get; set; }
    public int FilesIncluded { get; set; }
    public int FilesTruncated { get; set; }
    public int FilesSkipped { get; set; }
    public long TotalContextSize { get; set; }
    public long MaxContextSize { get; set; }
}

/// <summary>
/// Information about a single file in the context.
/// </summary>
public class ContextFileInfo
{
    public string RelativePath { get; set; } = "";
    public long OriginalSize { get; set; }
    public long TruncatedSize { get; set; }
    public bool WasTruncated { get; set; }
    
    public double TruncationPercent => OriginalSize > 0 
        ? (1.0 - (double)TruncatedSize / OriginalSize) * 100 
        : 0;
}
