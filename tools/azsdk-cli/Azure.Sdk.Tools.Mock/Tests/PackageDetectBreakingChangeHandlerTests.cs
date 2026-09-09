// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.CompilerServices;
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Models.SdkBreakingChangeDetection;
using Azure.Sdk.Tools.Cli.Tools.Package;
using Azure.Sdk.Tools.Mock.Handlers;
using Azure.Sdk.Tools.Mock.Handlers.Package;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using NUnit.Framework;

namespace Azure.Sdk.Tools.Mock.Tests;

[TestFixture]
public class PackageDetectBreakingChangeHandlerTests
{
    private readonly PackageDetectBreakingChangeHandler _handler = new();

    [TestCase("Azure.ResourceManager.Contoso", SdkLanguage.DotNet, SdkType.Management)]
    [TestCase("Azure.Contoso.Widget", SdkLanguage.DotNet, SdkType.Dataplane)]
    [TestCase("azure-resourcemanager-contoso", SdkLanguage.Java, SdkType.Management)]
    public void Handle_ClassifiedFixturePreservesMetadataAndAllChanges(string packageName, SdkLanguage language, SdkType sdkType)
    {
        var response = Invoke(packageName);
        var result = GetResult(response);

        Assert.Multiple(() =>
        {
            Assert.That(response.ExitCode, Is.Zero);
            Assert.That(response.BreakingChangeStatus, Is.EqualTo(SdkBreakingChangeStatus.Classified));
            Assert.That(response.PackageName, Is.EqualTo(packageName));
            Assert.That(response.Language, Is.EqualTo(language));
            Assert.That(response.PackageType, Is.EqualTo(sdkType));
            Assert.That(response.Version, Is.EqualTo("1.1.0-beta.1"));
            Assert.That(response.SdkRepoName, Is.EqualTo(SdkLanguageHelpers.GetRepoName(language)));
            Assert.That(result.HasBreakingChange, Is.True);
            Assert.That(result.SdkChangeMD, Does.Contain("### Breaking Changes").And.Contain("### Features Added"));
            Assert.That(result.BreakingChanges, Has.Count.EqualTo(1));
        });
        if (language == SdkLanguage.DotNet)
        {
            Assert.That(result.Details!.BaselineVersion, Is.EqualTo("1.0.0"));
            Assert.That(result.Details.ApiChanges.Select(change => change.Kind), Is.EqualTo(new[] { "removed", "added" }));
            Assert.That(result.Details.ApiChanges.Select(change => change.IsBreaking), Is.EqualTo(new[] { true, false }));
        }
        else
        {
            Assert.That(result.Details, Is.Null, "The Java fixture must not fabricate native .NET evidence.");
        }
        var breakingChange = result.BreakingChanges.Single();
        Assert.Multiple(() =>
        {
            Assert.That(breakingChange.Category, Is.EqualTo(SdkBreakingChangeCategory.Unknown));
            Assert.That(breakingChange.Resolution, Does.Contain("not a proven rename"));
            Assert.That(result.SdkChangeMD, Does.Contain(breakingChange.OriginBreaks!.Single()));
            Assert.That(breakingChange.Mitigation, Is.EqualTo(language == SdkLanguage.DotNet ? SdkBreakingChangeMitigation.Manual : null));
        });
    }

    [TestCase("Azure.ResourceManager.Contoso")]
    [TestCase("Azure.Contoso.Widget")]
    [TestCase("azure-resourcemanager-contoso")]
    public void Handle_NoBreaksKeepsAdditionsVisible(string packageName)
    {
        var response = Invoke(packageName, "mock-no-breaks");
        var result = GetResult(response);

        Assert.Multiple(() =>
        {
            Assert.That(response.ExitCode, Is.Zero);
            Assert.That(response.BreakingChangeStatus, Is.EqualTo(SdkBreakingChangeStatus.Clean));
            Assert.That(result.HasBreakingChange, Is.False);
            Assert.That(result.BreakingChanges, Is.Empty);
            Assert.That(result.SdkChangeMD, Does.Contain("### Features Added").And.Not.Contain("### Breaking Changes"));
            if (response.Language == SdkLanguage.DotNet)
            {
                Assert.That(result.Details!.ApiChanges.Single().Kind, Is.EqualTo("added"));
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Handle_ChangesOnlyPreservesRawEvidenceWithoutClassification(bool useJsonArguments)
    {
        var arguments = Arguments("Azure.Contoso.Widget");
        arguments["changesOnly"] = true;
        var response = Invoke(useJsonArguments ? ToWireArguments(arguments) : arguments);
        var result = GetResult(response);

        Assert.Multiple(() =>
        {
            Assert.That(response.ExitCode, Is.Zero);
            Assert.That(response.BreakingChangeStatus, Is.EqualTo(SdkBreakingChangeStatus.Detected));
            Assert.That(result.HasBreakingChange, Is.True);
            Assert.That(result.BreakingChanges, Is.Empty);
            Assert.That(result.Details!.ApiChanges, Has.Count.EqualTo(2));
            Assert.That(result.Details.Diagnostics.Single(), Does.Contain("CP0002"));
            Assert.That(response.Message, Does.Contain("without classification"));
        });
    }

    [Test]
    public void Handle_JsonArgumentsUseTheSameClassifiedContract()
    {
        var arguments = Arguments("Azure.ResourceManager.Contoso");
        arguments["changesOnly"] = false;

        var response = Invoke(ToWireArguments(arguments));

        Assert.That(GetResult(response).BreakingChanges.Single().Mitigation, Is.EqualTo(SdkBreakingChangeMitigation.Manual));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Handle_NoGaBaselineDoesNotClaimCompatibility(bool changesOnly)
    {
        var response = Invoke("Azure.Contoso.Widget", "mock-no-baseline", changesOnly);
        var result = GetResult(response);

        Assert.Multiple(() =>
        {
            Assert.That(response.Message, Does.Contain("Compatibility not evaluated"));
            Assert.That(response.BreakingChangeStatus, Is.EqualTo(SdkBreakingChangeStatus.Inconclusive));
            Assert.That(result.Details!.BaselineVersion, Is.Null);
            Assert.That(result.Details.Limitations.Single(), Does.Contain("no GA baseline"));
            Assert.That(result.BreakingChanges, Is.Empty);
        });
    }

    [TestCase("mock-classifier-error")]
    [TestCase("mock-catalog-error")]
    public void Handle_ClassificationFailureRetainsRawDetails(string scenario)
    {
        var response = Invoke("Azure.ResourceManager.Contoso", scenario);
        var result = GetResult(response);

        Assert.Multiple(() =>
        {
            Assert.That(response.ExitCode, Is.Not.Zero);
            Assert.That(response.BreakingChangeStatus, Is.EqualTo(SdkBreakingChangeStatus.Failed));
            Assert.That(response.ResponseErrors, Is.Not.Empty);
            Assert.That(result.HasBreakingChange, Is.True);
            Assert.That(result.SdkChangeMD, Does.Contain("CP0002").And.Contain("DisplayName"));
            Assert.That(result.Details!.ApiChanges, Has.Count.EqualTo(2));
            Assert.That(result.Details.Diagnostics.Single(), Does.Contain("CP0002"));
            Assert.That(result.BreakingChanges, Is.Empty);
        });
    }

    [TestCase("mock-classifier-error")]
    [TestCase("mock-catalog-error")]
    public void Handle_ChangesOnlyDoesNotEnterClassificationFailureScenario(string scenario)
    {
        var response = Invoke("Azure.Contoso.Widget", scenario, changesOnly: true);

        Assert.Multiple(() =>
        {
            Assert.That(response.ExitCode, Is.Zero);
            Assert.That(GetResult(response).HasBreakingChange, Is.True);
            Assert.That(GetResult(response).BreakingChanges, Is.Empty);
        });
    }

    [TestCase("mock-missing-artifacts", false)]
    [TestCase("mock-missing-artifacts", true)]
    [TestCase("mock-stale-artifacts", false)]
    [TestCase("mock-stale-artifacts", true)]
    public void Handle_UnavailableArtifactsFailExplicitly(string scenario, bool changesOnly)
    {
        var response = Invoke("Azure.Contoso.Widget", scenario, changesOnly);

        Assert.Multiple(() =>
        {
            Assert.That(response.ExitCode, Is.Not.Zero);
            Assert.That(response.BreakingChangeStatus, Is.EqualTo(SdkBreakingChangeStatus.Failed));
            Assert.That(response.ResponseErrors.Single(), Does.Contain("artifacts"));
            Assert.That(response.Result, Is.EqualTo("failed"));
        });
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase(42)]
    public void Handle_InvalidPackagePathFails(object? packagePath)
    {
        var response = Invoke(new Dictionary<string, object?> { ["packagePath"] = packagePath });

        Assert.That(response.ResponseErrors.Single(), Does.Contain("packagePath"));
    }

    [Test]
    public void Handle_NullArgumentsFail()
    {
        Assert.That(Invoke((Dictionary<string, object?>?)null).ExitCode, Is.Not.Zero);
    }

    [TestCase("Unknown.Package")]
    [TestCase("azure-mgmt-contoso")]
    public void Handle_UnconfiguredPackageDoesNotUseDefaultSuccess(string packageName)
    {
        var response = Invoke(packageName);

        Assert.That(response.ResponseErrors.Single(), Does.Contain("Unsupported mock package"));
    }

    [TestCase(null)]
    [TestCase("true")]
    [TestCase(1)]
    public void Handle_InvalidBooleanDoesNotDefaultToClassifiedSuccess(object? changesOnly)
    {
        var arguments = Arguments("Azure.Contoso.Widget");
        arguments["changesOnly"] = changesOnly;

        Assert.That(Invoke(arguments).ResponseErrors.Single(), Does.Contain("Boolean"));
    }

    [Test]
    public void Handle_ConflictingFixtureScenariosFail()
    {
        var arguments = Arguments("Azure.Contoso.Widget", @"mock-no-breaks\mock-catalog-error");

        Assert.That(Invoke(arguments).ResponseErrors.Single(), Does.Contain("only one mock scenario"));
    }

    [TestCase("tspConfigPath", 42)]
    [TestCase("tspConfigPath", true)]
    [TestCase("localSdkChangeJsonFilePath", 42)]
    [TestCase("localSdkChangeJsonFilePath", true)]
    public void Handle_InvalidOptionalPathTypesFail(string option, object value)
    {
        var arguments = Arguments("Azure.Contoso.Widget");
        arguments[option] = value;

        Assert.That(Invoke(arguments).ResponseErrors.Single(), Does.Contain(option));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Handle_OptionalNullablePathsDoNotReadSdkFiles(bool useJsonArguments)
    {
        var arguments = Arguments("Azure.Contoso.Widget");
        arguments["tspConfigPath"] = @"C:\not-read\tspconfig.yaml";
        arguments["localSdkChangeJsonFilePath"] = null;

        Assert.That(Invoke(useJsonArguments ? ToWireArguments(arguments) : arguments).ExitCode, Is.Zero);
    }

    [Test]
    public void Handle_LocalReplayReportsMissingMockSupportWithoutReadingFiles()
    {
        var arguments = Arguments("Azure.Contoso.Widget");
        arguments["localSdkChangeJsonFilePath"] = @"C:\not-read\sdk-changes.json";

        Assert.That(Invoke(arguments).ResponseErrors.Single(), Does.Contain("Local artifact replay is not implemented"));

        arguments["changesOnly"] = true;
        Assert.That(Invoke(arguments).ExitCode, Is.Zero);
    }

    [TestCase("C:\\sdk\\Azure.Contoso.Widget\\")]
    [TestCase("/sdk/Azure.Contoso.Widget/")]
    [TestCase("C:\\MOCK-NO-BREAKS\\azure.contoso.widget")]
    public void Handle_FixturePathsArePlatformIndependent(string packagePath)
    {
        Assert.That(Invoke(new Dictionary<string, object?> { ["packagePath"] = packagePath }).ExitCode, Is.Zero);
    }

    [Test]
    public void Handle_DetailsDoNotReplacePrimaryCompatibilityFieldsOrLeakAcrossCalls()
    {
        var first = GetResult(Invoke("Azure.Contoso.Widget"));
        first.Details!.ApiChanges.ForEach(change => change.IsBreaking = false);
        first.Details.Diagnostics.Clear();

        Assert.That(first.HasBreakingChange, Is.True);
        var second = GetResult(Invoke("Azure.Contoso.Widget"));
        Assert.Multiple(() =>
        {
            Assert.That(second.Details!.ApiChanges[0].IsBreaking, Is.True);
            Assert.That(second.Details.Diagnostics, Is.Not.Empty);
            Assert.That(second.SdkChangeMD, Is.EqualTo(first.SdkChangeMD));
        });
    }

    [TestCase("Azure.Contoso.Widget", true)]
    [TestCase("azure-resourcemanager-contoso", false)]
    public void Handle_SerializesTheRealCommonEnvelope(string packageName, bool hasMitigation)
    {
        var response = Invoke(packageName);
        var json = JsonSerializer.SerializeToElement(response);
        var result = json.GetProperty("result");

        Assert.Multiple(() =>
        {
            Assert.That(json.GetProperty("package_name").GetString(), Is.EqualTo(packageName));
            Assert.That(result.GetProperty("changes").ValueKind, Is.EqualTo(JsonValueKind.String));
            Assert.That(result.GetProperty("hasBreakingChange").GetBoolean(), Is.True);
            Assert.That(json.GetProperty("breaking_change_status").GetString(), Is.EqualTo("classified"));
            Assert.That(result.GetProperty("breakingChanges")[0].TryGetProperty("mitigation", out _), Is.EqualTo(hasMitigation));
        });
        if (hasMitigation)
        {
            Assert.That(result.GetProperty("details").GetProperty("baselineVersion").GetString(), Is.EqualTo("1.0.0"));
            Assert.That(result.GetProperty("details").GetProperty("apiChanges").GetArrayLength(), Is.EqualTo(2));
            Assert.That(result.GetProperty("breakingChanges")[0].GetProperty("mitigation").GetString(), Is.EqualTo("manual"));
        }
        else
        {
            Assert.That(result.TryGetProperty("details", out _), Is.False);
        }
    }

    [Test]
    public void Handle_MissingConfigurationIsDistinctFromToolFailure()
    {
        var response = Invoke("Azure.Contoso.Widget", "mock-missing-config");

        Assert.That(response.BreakingChangeStatus, Is.EqualTo(SdkBreakingChangeStatus.Blocked));
        Assert.That(response.ExitCode, Is.Not.Zero);
        Assert.That(response.ResponseErrors.Single(), Does.Contain("getSdkChangesScript"));
        Assert.That(response.Result, Is.EqualTo("failed"));
    }

    [Test]
    public void Registration_UsesRealDetectorSchemaAndDiscoversTheHandler()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<MockToolFactory>();
        MockToolRegistrations.RegisterMockMcpTools(services);
        using var provider = services.BuildServiceProvider();
        var mock = provider.GetServices<McpServerTool>().Single(tool => tool.ProtocolTool.Name == _handler.ToolName);
        var method = typeof(SdkBreakingChangeDetectTool).GetMethod(nameof(SdkBreakingChangeDetectTool.DetectSDKBreakingChangesAsync))!;
        var real = McpServerTool.Create(method,
            _ => RuntimeHelpers.GetUninitializedObject(typeof(SdkBreakingChangeDetectTool)),
            new McpServerToolCreateOptions { Services = provider });

        Assert.Multiple(() =>
        {
            Assert.That(mock.ProtocolTool.Name, Is.EqualTo(real.ProtocolTool.Name));
            Assert.That(mock.ProtocolTool.Description, Is.EqualTo(real.ProtocolTool.Description));
            Assert.That(mock.ProtocolTool.InputSchema.GetRawText(), Is.EqualTo(real.ProtocolTool.InputSchema.GetRawText()));
            Assert.That(mock.ProtocolTool.InputSchema.GetProperty("properties").EnumerateObject().Select(property => property.Name),
                Is.EquivalentTo(new[] { "packagePath", "tspConfigPath", "changesOnly", "localSdkChangeJsonFilePath" }));
            Assert.That(provider.GetRequiredService<MockToolFactory>().GetHandler(_handler.ToolName),
                Is.TypeOf<PackageDetectBreakingChangeHandler>());
        });
    }

    private PackageOperationResponse Invoke(string packageName, string? scenario = null, bool changesOnly = false)
    {
        var arguments = Arguments(packageName, scenario);
        arguments["changesOnly"] = changesOnly;
        return Invoke(arguments);
    }

    private PackageOperationResponse Invoke(Dictionary<string, object?>? arguments)
    {
        var response = _handler.Handle(arguments);
        Assert.That(response, Is.TypeOf<PackageOperationResponse>());
        return (PackageOperationResponse)response;
    }

    private static Dictionary<string, object?> Arguments(string packageName, string? scenario = null) =>
        new() { ["packagePath"] = $@"C:\sdk\{scenario ?? "default"}\{packageName}" };

    private static Dictionary<string, object?> ToWireArguments(Dictionary<string, object?> arguments) =>
        JsonSerializer.SerializeToElement(arguments).EnumerateObject()
            .ToDictionary(property => property.Name, property => (object?)property.Value);

    private static SdkBreakingChangeDetectionResult GetResult(PackageOperationResponse response)
    {
        Assert.That(response.Result, Is.TypeOf<SdkBreakingChangeDetectionResult>());
        return (SdkBreakingChangeDetectionResult)response.Result!;
    }
}
