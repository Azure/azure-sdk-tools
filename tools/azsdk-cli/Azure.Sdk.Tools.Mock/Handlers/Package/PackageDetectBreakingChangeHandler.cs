// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Models.SdkBreakingChangeDetection;

namespace Azure.Sdk.Tools.Mock.Handlers.Package;

/// <summary>Canned responses for azsdk_package_detect_breaking_change; never runs an SDK detector or classifier.</summary>
public class PackageDetectBreakingChangeHandler : IMockToolHandler
{
    private static readonly HashSet<string> ScenarioSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "mock-no-breaks",
        "mock-no-baseline",
        "mock-missing-artifacts",
        "mock-stale-artifacts",
        "mock-classifier-error",
        "mock-catalog-error",
        "mock-missing-config",
    };

    public string ToolName => "azsdk_package_detect_breaking_change";

    public CommandResponse Handle(Dictionary<string, object?>? arguments)
    {
        var response = HandleCore(arguments);
        response.BreakingChangeStatus ??= SdkBreakingChangeStatus.Failed;
        return response;
    }

    private static PackageOperationResponse HandleCore(Dictionary<string, object?>? arguments)
    {
        var packagePath = GetString(arguments?.GetValueOrDefault("packagePath"));
        if (string.IsNullOrWhiteSpace(packagePath))
        {
            return PackageOperationResponse.CreateFailure("The mock requires a non-empty string packagePath.");
        }

        var segments = packagePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var packageInfo = GetPackageInfo(segments.LastOrDefault(), packagePath);
        if (packageInfo is null)
        {
            return PackageOperationResponse.CreateFailure(
                "Unsupported mock package. Use Azure.ResourceManager.Contoso, Azure.Contoso.Widget, or azure-resourcemanager-contoso.");
        }

        if (!TryGetChangesOnly(arguments, out var changesOnly))
        {
            return PackageOperationResponse.CreateFailure("The mock requires changesOnly to be a Boolean.", packageInfo);
        }

        foreach (var option in new[] { "tspConfigPath", "localSdkChangeJsonFilePath" })
        {
            var value = arguments?.GetValueOrDefault(option);
            if (value is not (null or string or JsonElement { ValueKind: JsonValueKind.String or JsonValueKind.Null }))
            {
                return PackageOperationResponse.CreateFailure($"The mock requires {option} to be a string or null.", packageInfo);
            }
        }

        if (!changesOnly && !string.IsNullOrEmpty(GetString(arguments?.GetValueOrDefault("localSdkChangeJsonFilePath"))))
        {
            return PackageOperationResponse.CreateFailure(
                "Local artifact replay is not implemented by this mock. Use the documented packagePath fixture scenarios.", packageInfo);
        }

        var scenarios = segments.Where(ScenarioSegments.Contains).ToArray();
        if (scenarios.Length > 1)
        {
            return PackageOperationResponse.CreateFailure("Select only one mock scenario path segment.", packageInfo);
        }
        var scenario = scenarios.SingleOrDefault()?.ToLowerInvariant();
        if (scenario == "mock-missing-config")
        {
            var blocked = PackageOperationResponse.CreateFailure(
                "Required packageOptions.getSdkChangesScript configuration is absent (mock fixture).", packageInfo);
            blocked.BreakingChangeStatus = SdkBreakingChangeStatus.Blocked;
            return blocked;
        }
        if (scenario is "mock-missing-artifacts" or "mock-stale-artifacts")
        {
            return PackageOperationResponse.CreateFailure(
                scenario == "mock-missing-artifacts"
                    ? "Current comparison artifacts are missing (mock fixture)."
                    : "Comparison artifacts are stale (mock fixture).",
                packageInfo);
        }

        var response = PackageOperationResponse.CreateSuccess(
            "SDK breaking changes detected and classified (mock fixture).",
            packageInfo,
            sdkRepoName: SdkLanguageHelpers.GetRepoName(packageInfo.Language));

        if (scenario == "mock-no-baseline")
        {
            const string limitation = "Compatibility not evaluated: no GA baseline is available (mock fixture).";
            response.Message = limitation;
            response.BreakingChangeStatus = SdkBreakingChangeStatus.Inconclusive;
            response.Result = new SdkBreakingChangeDetectionResult
            {
                SdkChangeMD = limitation,
                Details = new DotnetSdkChangeDetails { Limitations = [limitation] },
            };
            return response;
        }

        var result = CreateChanges(packageInfo, scenario == "mock-no-breaks");
        response.Result = result;
        if (!result.HasBreakingChange)
        {
            response.BreakingChangeStatus = SdkBreakingChangeStatus.Clean;
            response.Message = "No breaking changes detected; one API addition remains visible (mock fixture).";
        }
        else if (changesOnly)
        {
            response.BreakingChangeStatus = SdkBreakingChangeStatus.Detected;
            response.Message = "SDK changes detected without classification (mock fixture).";
        }
        else if (scenario is "mock-classifier-error" or "mock-catalog-error")
        {
            response.ResponseError = scenario == "mock-classifier-error"
                ? "Failed to classify SDK breaking changes (mock fixture)."
                : "Failed to load the configured SDK breaking-change catalog (mock fixture).";
            response.Message = "Raw detector evidence is preserved; classification is incomplete (mock fixture).";
        }
        else
        {
            response.BreakingChangeStatus = SdkBreakingChangeStatus.Classified;
            result.BreakingChanges =
            [
                new SdkBreakingChange
                {
                    BreakingChange = $"Public API '{RemovedSymbol(packageInfo)}' was removed.",
                    Category = SdkBreakingChangeCategory.Unknown,
                    Resolution = "The removal and addition are not a proven rename. Root-cause confidence is low; request owner judgment before mitigation.",
                    MitigationStrategy = packageInfo.Language == SdkLanguage.DotNet ? SdkBreakingChangeMitigationStrategy.Manual : null,
                    OriginBreaks = [RemovalDiagnostic(packageInfo)],
                },
            ];
        }
        return response;
    }

    private static string RemovedSymbol(PackageInfo packageInfo) => packageInfo.Language == SdkLanguage.DotNet
        ? $"{packageInfo.PackageName}.Widget.Name"
        : "com.azure.resourcemanager.contoso.models.Widget.name()";

    private static string RemovalDiagnostic(PackageInfo packageInfo) =>
        $"{(packageInfo.Language == SdkLanguage.DotNet ? "CP0002" : "MOCK001")}: Public API '{RemovedSymbol(packageInfo)}' was removed.";

    private static PackageInfo? GetPackageInfo(string? packageName, string packagePath)
    {
        var (language, sdkType) = packageName?.ToLowerInvariant() switch
        {
            "azure.resourcemanager.contoso" => (SdkLanguage.DotNet, SdkType.Management),
            "azure.contoso.widget" => (SdkLanguage.DotNet, SdkType.Dataplane),
            "azure-resourcemanager-contoso" => (SdkLanguage.Java, SdkType.Management),
            _ => (SdkLanguage.Unknown, SdkType.Unknown),
        };
        return language == SdkLanguage.Unknown ? null : new PackageInfo
        {
            PackageName = packageName,
            PackagePath = packagePath,
            PackageVersion = "1.1.0-beta.1",
            Language = language,
            SdkType = sdkType,
        };
    }

    private static SdkBreakingChangeDetectionResult CreateChanges(PackageInfo packageInfo, bool additionsOnly)
    {
        var isDotnet = packageInfo.Language == SdkLanguage.DotNet;
        var removedSymbol = RemovedSymbol(packageInfo);
        var addedSymbol = isDotnet
            ? $"{packageInfo.PackageName}.Widget.DisplayName"
            : "com.azure.resourcemanager.contoso.models.Widget.displayName()";
        var diagnosticId = isDotnet ? "CP0002" : "MOCK001";
        var diagnostic = RemovalDiagnostic(packageInfo);
        var addition = $"Public API '{addedSymbol}' was added.";
        var details = new DotnetSdkChangeDetails { BaselineVersion = "1.0.0" };
        if (!additionsOnly)
        {
            details.Diagnostics.Add(diagnostic);
            details.ApiChanges.Add(new DotnetSdkApiChange
            {
                Kind = "removed",
                Symbol = removedSymbol,
                Description = diagnostic,
                IsBreaking = true,
                DiagnosticId = diagnosticId,
                TargetFramework = isDotnet ? "net8.0" : null,
            });
        }
        details.ApiChanges.Add(new DotnetSdkApiChange
        {
            Kind = "added",
            Symbol = addedSymbol,
            Description = addition,
            IsBreaking = false,
            TargetFramework = isDotnet ? "net8.0" : null,
        });
        return new SdkBreakingChangeDetectionResult
        {
            HasBreakingChange = !additionsOnly,
            SdkChangeMD = (additionsOnly ? "" : $"### Breaking Changes\n\n- {diagnostic}\n\n")
                + $"### Features Added\n\n- {addition}",
            Details = isDotnet ? details : null,
        };
    }

    private static string? GetString(object? value) => value switch
    {
        string text => text,
        JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
        _ => null,
    };

    private static bool TryGetChangesOnly(Dictionary<string, object?>? arguments, out bool changesOnly)
    {
        changesOnly = false;
        if (arguments is null || !arguments.TryGetValue("changesOnly", out var value))
        {
            return true;
        }
        switch (value)
        {
            case bool flag:
                changesOnly = flag;
                return true;
            case JsonElement element when element.ValueKind is JsonValueKind.True or JsonValueKind.False:
                changesOnly = element.GetBoolean();
                return true;
            default:
                return false;
        }
    }
}
