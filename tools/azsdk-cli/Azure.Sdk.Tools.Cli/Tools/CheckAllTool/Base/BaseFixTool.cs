// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Models;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.CheckAllTool.Base
{
    /// <summary>
    /// Base class for MCP-only fix tools that automatically remediate issues.
    /// </summary>
    [McpServerToolType]
    public abstract class BaseFixTool : MCPTool
    {
        protected readonly ILogger logger;

        protected BaseFixTool(ILogger logger) : base()
        {
            this.logger = logger;
        }

        public override Command GetCommand()
        {
            // MCP-only tool - no CLI command
            return null!;
        }

        public override Task HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            // MCP-only tool - no CLI command handling
            throw new NotImplementedException("This tool is available only through MCP server interface");
        }

        /// <summary>
        /// Validates the project path exists.
        /// </summary>
        /// <param name="projectPath">Path to validate</param>
        /// <returns>Error message if invalid, null if valid</returns>
        protected string? ValidateProjectPath(string projectPath)
        {
            if (!Directory.Exists(projectPath))
            {
                return $"Project path does not exist: {projectPath}";
            }
            return null;
        }

        /// <summary>
        /// Creates a successful response for fix operations.
        /// </summary>
        /// <param name="message">Success message</param>
        /// <param name="duration">Duration in milliseconds</param>
        /// <param name="result">Optional result object</param>
        /// <returns>DefaultCommandResponse instance</returns>
        protected DefaultCommandResponse CreateSuccessResponse(string message, int duration, object? result = null)
        {
            return new DefaultCommandResponse
            {
                Message = message,
                Duration = duration,
                Result = result
            };
        }

        /// <summary>
        /// Creates a failure response for fix operations.
        /// </summary>
        /// <param name="errorMessage">Error message</param>
        /// <param name="duration">Duration in milliseconds</param>
        /// <returns>DefaultCommandResponse instance</returns>
        protected DefaultCommandResponse CreateFailureResponse(string errorMessage, int duration)
        {
            SetFailure(1);
            return new DefaultCommandResponse
            {
                ResponseError = errorMessage,
                Duration = duration
            };
        }

        /// <summary>
        /// Creates an error response for exceptions.
        /// </summary>
        /// <param name="ex">Exception that occurred</param>
        /// <param name="duration">Duration in milliseconds</param>
        /// <returns>DefaultCommandResponse instance</returns>
        protected DefaultCommandResponse CreateExceptionResponse(Exception ex, int duration)
        {
            logger.LogError(ex, $"Unhandled exception while running {GetFixType().ToLower()} fixes");
            SetFailure(1);
            return new DefaultCommandResponse
            {
                ResponseError = $"Unhandled exception: {ex.Message}",
                Duration = duration
            };
        }

        /// <summary>
        /// Executes the fix operation with common error handling and timing.
        /// </summary>
        /// <param name="projectPath">Project path to fix</param>
        /// <returns>Response with timing and error handling</returns>
        public async Task<DefaultCommandResponse> RunFix(string projectPath)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                logger.LogInformation($"Starting {GetFixType().ToLower()} fixes for project at: {projectPath}");
                
                var pathError = ValidateProjectPath(projectPath);
                if (pathError != null)
                {
                    stopwatch.Stop();
                    return CreateFailureResponse(pathError, (int)stopwatch.ElapsedMilliseconds);
                }

                var result = await ExecuteFix(projectPath);
                stopwatch.Stop();
                
                return result.Success 
                    ? CreateSuccessResponse(result.Message, (int)stopwatch.ElapsedMilliseconds, result.Result)
                    : CreateFailureResponse(result.ErrorMessage ?? "Fix operation failed", (int)stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return CreateExceptionResponse(ex, (int)stopwatch.ElapsedMilliseconds);
            }
        }

        /// <summary>
        /// Executes the specific fix logic. Override in derived classes.
        /// </summary>
        /// <param name="projectPath">Project path to fix</param>
        /// <returns>Fix result</returns>
        protected abstract Task<(bool Success, string Message, string? ErrorMessage, object? Result)> ExecuteFix(string projectPath);

        /// <summary>
        /// Gets the fix type name for logging. Override in derived classes.
        /// </summary>
        /// <returns>Fix type name</returns>
        protected abstract string GetFixType();
    }
}