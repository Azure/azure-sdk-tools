// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Models;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.CheckAllTool.Base
{
    /// <summary>
    /// Base class for validation tools that provide both CLI and MCP interfaces.
    /// </summary>
    [McpServerToolType]
    public abstract class BaseValidationTool : MCPTool
    {
        protected readonly ILogger logger;
        protected readonly IOutputService output;
        
        protected readonly Option<string> projectPathOption = new(["--project-path", "-p"], "Path to the project directory to check") { IsRequired = true };

        protected BaseValidationTool(ILogger logger, IOutputService output) : base()
        {
            this.logger = logger;
            this.output = output;
            CommandHierarchy = [SharedCommandGroups.Checks];
        }

        public override Command GetCommand()
        {
            Command command = new(GetCommandName(), GetCommandDescription());
            command.AddOption(projectPathOption);
            command.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
            return command;
        }

        public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            try
            {
                var projectPath = ctx.ParseResult.GetValueForOption(projectPathOption);
                var result = await RunValidation(projectPath);

                output.Output(result);
                ctx.ExitCode = ExitCode;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Error occurred while running {GetCheckType().ToLower()}");
                SetFailure(1);
                output.Output(new DefaultCommandResponse
                {
                    ResponseError = $"Error occurred while running {GetCheckType().ToLower()}: {ex.Message}"
                });
                ctx.ExitCode = ExitCode;
            }
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
        /// Creates a successful CheckResult.
        /// </summary>
        /// <param name="duration">Duration in milliseconds</param>
        /// <param name="details">Optional details</param>
        /// <returns>CheckResult instance</returns>
        protected CheckResult CreateSuccessResult(int duration, List<string>? details = null)
        {
            return new CheckResult
            {
                CheckType = GetCheckType(),
                Success = true,
                Message = $"{GetCheckType()} completed successfully",
                Duration = duration,
                Details = details
            };
        }

        /// <summary>
        /// Creates a failed CheckResult.
        /// </summary>
        /// <param name="errorMessage">Error message</param>
        /// <param name="duration">Duration in milliseconds</param>
        /// <param name="details">Optional details</param>
        /// <returns>CheckResult instance</returns>
        protected CheckResult CreateFailureResult(string errorMessage, int duration, List<string>? details = null)
        {
            return new CheckResult
            {
                CheckType = GetCheckType(),
                Success = false,
                Message = errorMessage,
                Duration = duration,
                Details = details
            };
        }

        /// <summary>
        /// Creates a successful response with CheckResult.
        /// </summary>
        /// <param name="duration">Duration in milliseconds</param>
        /// <param name="details">Optional details</param>
        /// <returns>DefaultCommandResponse instance</returns>
        protected DefaultCommandResponse CreateSuccessResponse(int duration, List<string>? details = null)
        {
            var result = CreateSuccessResult(duration, details);
            return new DefaultCommandResponse
            {
                Message = result.Message,
                Duration = duration,
                Result = result
            };
        }

        /// <summary>
        /// Creates a failure response with CheckResult.
        /// </summary>
        /// <param name="errorMessage">Error message</param>
        /// <param name="duration">Duration in milliseconds</param>
        /// <param name="details">Optional details</param>
        /// <returns>DefaultCommandResponse instance</returns>
        protected DefaultCommandResponse CreateFailureResponse(string errorMessage, int duration, List<string>? details = null)
        {
            SetFailure(1);
            var result = CreateFailureResult(errorMessage, duration, details);
            return new DefaultCommandResponse
            {
                ResponseError = errorMessage,
                Duration = duration,
                Result = result
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
            logger.LogError(ex, $"Unhandled exception while running {GetCheckType().ToLower()}");
            SetFailure(1);
            return new DefaultCommandResponse
            {
                ResponseError = $"Unhandled exception: {ex.Message}",
                Duration = duration
            };
        }

        /// <summary>
        /// Executes the validation with common error handling and timing.
        /// </summary>
        /// <param name="projectPath">Project path to validate</param>
        /// <returns>Response with timing and error handling</returns>
        public async Task<DefaultCommandResponse> RunValidation(string projectPath)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                logger.LogInformation($"Starting {GetCheckType().ToLower()} for project at: {projectPath}");
                
                var pathError = ValidateProjectPath(projectPath);
                if (pathError != null)
                {
                    stopwatch.Stop();
                    return CreateFailureResponse(pathError, (int)stopwatch.ElapsedMilliseconds);
                }

                var result = await ExecuteValidation(projectPath);
                stopwatch.Stop();
                
                return result.Success 
                    ? CreateSuccessResponse((int)stopwatch.ElapsedMilliseconds, result.Details)
                    : CreateFailureResponse(result.ErrorMessage ?? "Validation failed", (int)stopwatch.ElapsedMilliseconds, result.Details);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return CreateExceptionResponse(ex, (int)stopwatch.ElapsedMilliseconds);
            }
        }

        /// <summary>
        /// Executes the specific validation logic. Override in derived classes.
        /// </summary>
        /// <param name="projectPath">Project path to validate</param>
        /// <returns>Validation result</returns>
        protected abstract Task<(bool Success, string? ErrorMessage, List<string>? Details)> ExecuteValidation(string projectPath);

        /// <summary>
        /// Gets the CLI command name. Override in derived classes.
        /// </summary>
        /// <returns>Command name for CLI</returns>
        protected abstract string GetCommandName();

        /// <summary>
        /// Gets the CLI command description. Override in derived classes.
        /// </summary>
        /// <returns>Command description for CLI</returns>
        protected abstract string GetCommandDescription();

        /// <summary>
        /// Gets the check type name for logging and results. Override in derived classes.
        /// </summary>
        /// <returns>Check type name</returns>
        protected abstract string GetCheckType();
    }
}