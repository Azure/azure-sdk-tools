// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;

namespace Azure.Sdk.Tools.Mock.Handlers.Package;

internal static class PackageMockResponses
{
    public static PackageInfo ContosoPackage(string? language = null) => new()
    {
        PackageName = "Azure.Template.Contoso",
        PackageVersion = "1.0.0-beta.1",
        Language = language is null ? SdkLanguage.DotNet : SdkLanguageHelpers.GetSdkLanguage(language),
        SdkType = SdkType.Dataplane
    };
}

/// <summary>Mock handler for azsdk_package_generate_code.</summary>
public class PackageGenerateCodeHandler : IMockToolHandler
{
    public string ToolName => "azsdk_package_generate_code";
    public CommandResponse Handle(Dictionary<string, object?>? arguments) =>
        PackageOperationResponse.CreateSuccess(
            "SDK code generated successfully (mock)",
            PackageMockResponses.ContosoPackage(arguments?.GetValueOrDefault("language")?.ToString()),
            typespecProjectPath: arguments?.GetValueOrDefault("typeSpecProjectPath")?.ToString());
}

/// <summary>Mock handler for azsdk_package_build_code.</summary>
public class PackageBuildCodeHandler : IMockToolHandler
{
    public string ToolName => "azsdk_package_build_code";
    public CommandResponse Handle(Dictionary<string, object?>? arguments) =>
        PackageOperationResponse.CreateSuccess(
            "Package built successfully (mock)",
            PackageMockResponses.ContosoPackage(arguments?.GetValueOrDefault("language")?.ToString()));
}

/// <summary>Mock handler for azsdk_package_run_tests.</summary>
public class PackageRunTestsHandler : IMockToolHandler
{
    public string ToolName => "azsdk_package_run_tests";
    public CommandResponse Handle(Dictionary<string, object?>? arguments) =>
        new TestRunResponse(exitCode: 0, testRunOutput: "Test run successful. 42 passed, 0 failed (mock).");
}

/// <summary>Mock handler for azsdk_package_run_check.</summary>
public class PackageRunCheckHandler : IMockToolHandler
{
    public string ToolName => "azsdk_package_run_check";
    public CommandResponse Handle(Dictionary<string, object?>? arguments) =>
        new PackageCheckResponse(
            packageName: "Azure.Template.Contoso",
            language: SdkLanguage.DotNet,
            exitCode: 0,
            checkStatusDetails: "All checks passed (mock).");
}

/// <summary>Mock handler for azsdk_package_pack.</summary>
public class PackagePackHandler : IMockToolHandler
{
    public string ToolName => "azsdk_package_pack";
    public CommandResponse Handle(Dictionary<string, object?>? arguments) =>
        PackageOperationResponse.CreateSuccess(
            "Package archive created (mock)",
            PackageMockResponses.ContosoPackage(arguments?.GetValueOrDefault("language")?.ToString()));
}

/// <summary>Mock handler for azsdk_package_update_version.</summary>
public class PackageUpdateVersionHandler : IMockToolHandler
{
    public string ToolName => "azsdk_package_update_version";
    public CommandResponse Handle(Dictionary<string, object?>? arguments) =>
        PackageOperationResponse.CreateSuccess(
            $"Version updated to {arguments?.GetValueOrDefault("newVersion")?.ToString() ?? "1.0.0-beta.2"} (mock)",
            PackageMockResponses.ContosoPackage(arguments?.GetValueOrDefault("language")?.ToString()));
}

/// <summary>Mock handler for azsdk_package_update_metadata.</summary>
public class PackageUpdateMetadataHandler : IMockToolHandler
{
    public string ToolName => "azsdk_package_update_metadata";
    public CommandResponse Handle(Dictionary<string, object?>? arguments) =>
        PackageOperationResponse.CreateSuccess(
            "Metadata updated (mock)",
            PackageMockResponses.ContosoPackage(arguments?.GetValueOrDefault("language")?.ToString()));
}

/// <summary>Mock handler for azsdk_package_update_changelog_content.</summary>
public class PackageUpdateChangelogContentHandler : IMockToolHandler
{
    public string ToolName => "azsdk_package_update_changelog_content";
    public CommandResponse Handle(Dictionary<string, object?>? arguments) =>
        PackageOperationResponse.CreateSuccess(
            "Changelog updated (mock)",
            PackageMockResponses.ContosoPackage(arguments?.GetValueOrDefault("language")?.ToString()));
}

/// <summary>Mock handler for azsdk_package_generate_samples.</summary>
public class PackageGenerateSamplesHandler : IMockToolHandler
{
    public string ToolName => "azsdk_package_generate_samples";
    public CommandResponse Handle(Dictionary<string, object?>? arguments)
    {
        var resp = PackageOperationResponse.CreateSuccess(
            "Samples generated (mock)",
            PackageMockResponses.ContosoPackage(arguments?.GetValueOrDefault("language")?.ToString()));
        resp.Result = new { samples_count = 3 };
        return resp;
    }
}

/// <summary>Mock handler for azsdk_package_translate_samples.</summary>
public class PackageTranslateSamplesHandler : IMockToolHandler
{
    public string ToolName => "azsdk_package_translate_samples";
    public CommandResponse Handle(Dictionary<string, object?>? arguments)
    {
        var resp = PackageOperationResponse.CreateSuccess(
            "Samples translated (mock)",
            PackageMockResponses.ContosoPackage(arguments?.GetValueOrDefault("language")?.ToString()));
        resp.Result = new { samples_count = 3 };
        return resp;
    }
}
