// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.ComponentModel;
using System.IO.Enumeration;
using System.Reflection;
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Telemetry;
using Azure.Sdk.Tools.Cli.Tools.Core;
using Azure.Sdk.Tools.Cli.Tools.TypeSpec;
using Microsoft.Extensions.AI;
using Microsoft.TeamFoundation.TestManagement.WebApi;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.CopilotAgents.Tools;

/// <summary>
/// Factory methods for creating TypeSpec-related AIFunction tools for copilot agents.
/// </summary>
public static class TypeSpecTools
{
    /// <summary>
    /// Creates a CompileTypeSpec tool that compiles a TypeSpec project to validate definitions.
    /// </summary>
    /// <param name="workingDirectory">The TypeSpec project directory.</param>
    /// <param name="npxHelper">The npx helper for running TypeSpec compiler.</param>
    /// <param name="entryPoint">The TypeSpec entry point file (default: "./client.tsp").</param>
    /// <param name="timeout">Compilation timeout (default: 2 minutes).</param>
    /// <returns>An AIFunction that compiles TypeSpec projects.</returns>
    public static AIFunction CreateCompileTypeSpecTool(
        string workingDirectory,
        INpxHelper npxHelper,
        string entryPoint = "./client.tsp",
        TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromMinutes(2);

        return AIFunctionFactory.Create(
            async (CancellationToken ct) =>
            {
                try
                {
                    var npxOptions = new NpxOptions(
                        package: "@typespec/compiler",
                        args: ["tsp", "compile", entryPoint, "--dry-run"],
                        logOutputStream: true,
                        workingDirectory: workingDirectory,
                        timeout: timeout.Value
                    );

                    var result = await npxHelper.Run(npxOptions, ct);

                    if (result.ExitCode == 0)
                    {
                        return new CompileTypeSpecResult
                        {
                            Success = true,
                            Output = string.IsNullOrWhiteSpace(result.Output) ? "Compilation succeeded" : result.Output
                        };
                    }

                    return new CompileTypeSpecResult
                    {
                        Success = false,
                        Output = result.Output
                    };
                }
                catch (OperationCanceledException)
                {
                    return new CompileTypeSpecResult
                    {
                        Success = false,
                        Output = $"Compilation timed out after {timeout.Value.TotalMinutes} minutes"
                    };
                }
                catch (Exception ex)
                {
                    return new CompileTypeSpecResult
                    {
                        Success = false,
                        Output = $"Failed to compile TypeSpec project: {ex.Message}"
                    };
                }
            },
            "CompileTypeSpec",
            "Compile the TypeSpec project to validate there are no errors in the TypeSpec definitions");
    }


    public static AIFunction CreateTypeSpecAuthoringTool(
        TypeSpecAuthoringTool toolInstance)
    {
        ArgumentNullException.ThrowIfNull(toolInstance);

        var toolType = typeof(TypeSpecAuthoringTool);
        var toolMethods = toolType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
                                    .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() is not null);
        var toolMethod = toolMethods.First(m => m.Name == nameof(TypeSpecAuthoringTool.GenerateTypeSpecAuthoringPlan));
        var mcpToolAttr = toolMethod.GetCustomAttribute<McpServerToolAttribute>();
        var toolName = mcpToolAttr?.Name ?? toolMethod.Name;
        var description = toolMethod.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "TypeSpec authoring tool";

        return AIFunctionFactory.Create(
            toolMethod,
            toolInstance,  // Pass the target instance
            toolName,
            description);
    }

    /// <summary>
    /// Result of a TypeSpec compilation.
    /// </summary>
    public class CompileTypeSpecResult
    {
        public bool Success { get; set; }
        public string Output { get; set; } = string.Empty;
    }
}
