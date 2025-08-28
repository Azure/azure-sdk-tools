// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Languages.Test;
using Azure.Sdk.Tools.Cli.Microagents;
using Azure.Sdk.Tools.Cli.Microagents.Tools;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services.Languages.Test;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.Package;

[McpServerToolType, Description("Records tests and provides suggestions to fix test failures")]
public class RecordTestsTool(ITestRunnerResolver testRunnerResolver, IGitHelper gitHelper, IOutputHelper output, ILogger<RecordTestsTool> logger, IMicroagentHostService agentHost) : MCPTool()
{
    // TODO extract to a separate file once we have the prompt infrastructure in place
    private const string GlobalSanitizerReference = """
        ## Complete Central Sanitizer Reference

        ### General Regex Sanitizers (1XXX series)

        - `AZSDK0000`: Basic Authorization header sanitizer (core RecordedTestSanitizer)
        - `AZSDK1000`: SharedAccessKey in connection strings
        - `AZSDK1001`: AccountKey in connection strings (replaces with BASE64ZERO)
        - `AZSDK1002`: accesskey (lowercase) in strings
        - `AZSDK1003`: Accesskey (capitalized) in strings
        - `AZSDK1004`: Secret= in connection strings
        - `AZSDK1005`: ACS Identity realm patterns (common/userrealm/)
        - `AZSDK1006`: ACS Identity patterns (/identities/)
        - `AZSDK1007`: Common SAS URL parameters (sig, sv) - applies to headers, URIs, and bodies
        - `AZSDK1008`: Token parameters in URLs

        ### Header Regex Sanitizers (2XXX series)

        - `AZSDK2001`: api-key header
        - `AZSDK2002`: x-ms-encryption-key header
        - `AZSDK2003`: Location header (replaces with "https://example.com")
        - `AZSDK2004`: subscription-key header
        - `AZSDK2005`: SupplementaryAuthorization header
        - `AZSDK2006`: x-ms-rename-source header
        - `AZSDK2007`: x-ms-file-rename-source header
        - `AZSDK2008`: x-ms-copy-source header
        - `AZSDK2009`: x-ms-copy-source-authorization header
        - `AZSDK2010`: x-ms-file-rename-source-authorization header
        - `AZSDK2011`: x-ms-encryption-key-sha256 header
        - `AZSDK2012`: aeg-sas-token header
        - `AZSDK2013`: aeg-sas-key header
        - `AZSDK2014`: aeg-channel-name header
        - `AZSDK2015`: Set-Cookie header
        - `AZSDK2016`: Cookie header
        - `AZSDK2017`: client-request-id header
        - `AZSDK2018`: MS-CV header
        - `AZSDK2019`: X-Azure-Ref header
        - `AZSDK2020`: x-ms-request-id header
        - `AZSDK2021`: x-ms-client-request-id header
        - `AZSDK2022`: x-ms-content-sha256 header
        - `AZSDK2023`: Content-Security-Policy-Report-Only header
        - `AZSDK2024`: Repeatability-First-Sent header
        - `AZSDK2025`: Repeatability-Request-ID header
        - `AZSDK2026`: repeatability-request-id header (lowercase)
        - `AZSDK2027`: repeatability-first-sent header (lowercase)
        - `AZSDK2028`: P3P header
        - `AZSDK2029`: x-ms-ests-server header
        - `AZSDK2030`: operation-location header (replaces with "https://example.com")
        - `AZSDK2031`: Ocp-Apim-Subscription-Key header

        ### Body Regex Sanitizers (3XXX series)

        - `AZSDK3000`: client_id parameters in request bodies
        - `AZSDK3001`: client_secret parameters in request bodies
        - `AZSDK3002`: client_assertion parameters in request bodies
        - `AZSDK3004`: Private key certificates (-----BEGIN PRIVATE KEY-----)
        - `AZSDK3005`: UserDelegationKey Value elements in XML
        - `AZSDK3006`: UserDelegationKey SignedTid elements in XML
        - `AZSDK3007`: UserDelegationKey SignedOid elements in XML
        - `AZSDK3008`: Password in connection strings (Password=)
        - `AZSDK3009`: User ID in connection strings (User ID=)
        - `AZSDK3010`: PrimaryKey XML elements
        - `AZSDK3011`: SecondaryKey XML elements
        - `AZSDK3012`: ClientIp XML elements

        ### Body Key Sanitizers (3400+ series) - JSON Path based

        - `AZSDK3400`: $..access_token
        - `AZSDK3401`: $..refresh_token
        - `AZSDK3402`: $..containerUrl
        - `AZSDK3403`: $..applicationSecret
        - `AZSDK3404`: $..apiKey
        - `AZSDK3405`: $..connectionString
        - `AZSDK3406`: $..sshPassword
        - `AZSDK3407`: $..aliasSecondaryConnectionString
        - `AZSDK3408`: $..primaryKey
        - `AZSDK3409`: $..secondaryKey
        - `AZSDK3410`: $..adminPassword.value
        - `AZSDK3411`: $..administratorLoginPassword
        - `AZSDK3412`: $..accessToken
        - `AZSDK3413`: $..runAsPassword
        - `AZSDK3414`: $..adminPassword
        - `AZSDK3415`: $..accessSAS
        - `AZSDK3416`: $..WEBSITE_AUTH_ENCRYPTION_KEY
        - `AZSDK3417`: $..decryptionKey
        - `AZSDK3418`: $..access_token (duplicate)
        - `AZSDK3419`: $..AccessToken
        - `AZSDK3420`: $..targetResourceId
        - `AZSDK3421`: $..urlSource
        - `AZSDK3422`: $..azureBlobSource.containerUrl
        - `AZSDK3423`: $..source
        - `AZSDK3424`: $..to
        - `AZSDK3425`: $..from
        - `AZSDK3426`: $..outputDataUri
        - `AZSDK3427`: $..inputDataUri
        - `AZSDK3428`: $..containerUri
        - `AZSDK3429`: $..sasUri (with SAS signature extraction)
        - `AZSDK3430`: $..id
        - `AZSDK3431`: $..token
        - `AZSDK3432`: $..appId
        - `AZSDK3433`: $..userId
        - `AZSDK3435`: $..storageAccount
        - `AZSDK3436`: $..resourceGroup
        - `AZSDK3437`: $..guardian
        - `AZSDK3438`: $..scan
        - `AZSDK3439`: $..catalog
        - `AZSDK3440`: $..lastModifiedBy
        - `AZSDK3441`: $..managedResourceGroupName
        - `AZSDK3442`: $..createdBy
        - `AZSDK3443`: $..tenantId (replaces with EMPTYGUID)
        - `AZSDK3444`: $..principalId (replaces with EMPTYGUID)
        - `AZSDK3445`: $..clientId (replaces with EMPTYGUID)
        - `AZSDK3446`: $..credential
        - `AZSDK3447`: $.key
        - `AZSDK3448`: $.value[*].key
        - `AZSDK3449`: $..uploadUrl
        - `AZSDK3450`: $..logLink
        - `AZSDK3451`: $..storageContainerUri
        - `AZSDK3452`: $..storageContainerReadListSas
        - `AZSDK3453`: $..storageContainerWriteSas
        - `AZSDK3454`: $..primaryMasterKey
        - `AZSDK3455`: $..primaryReadonlyMasterKey
        - `AZSDK3456`: $..secondaryMasterKey
        - `AZSDK3457`: $..secondaryReadonlyMasterKey
        - `AZSDK3458`: $..password
        - `AZSDK3459`: $..certificatePassword
        - `AZSDK3460`: $..clientSecret
        - `AZSDK3461`: $..keyVaultClientSecret
        - `AZSDK3462`: $..accountKey
        - `AZSDK3463`: $..authHeader
        - `AZSDK3464`: $..httpHeader
        - `AZSDK3465`: $..encryptedCredential
        - `AZSDK3466`: $..appkey
        - `AZSDK3467`: $..functionKey
        - `AZSDK3468`: $..atlasKafkaPrimaryEndpoint
        - `AZSDK3469`: $..atlasKafkaSecondaryEndpoint
        - `AZSDK3470`: $..certificatePassword
        - `AZSDK3471`: $..storageAccountPrimaryKey
        - `AZSDK3472`: $..privateKey
        - `AZSDK3473`: $..fencingClientPassword
        - `AZSDK3474`: $..acrToken
        - `AZSDK3475`: $..scriptUrlSasToken
        - `AZSDK3477`: $..accountKey
        - `AZSDK3478`: $..accountName
        - `AZSDK3479`: $..applicationId (replaces with EMPTYGUID)
        - `AZSDK3480`: $..apiKey
        - `AZSDK3482`: $..password
        - `AZSDK3483`: $..userName
        - `AZSDK3484`: $.properties.WEBSITE_AUTH_ENCRYPTION_KEY
        - `AZSDK3485`: $.properties.siteConfig.machineKey.decryptionKey
        - `AZSDK3486`: $.properties.DOCKER_REGISTRY_SERVER_PASSWORD
        - `AZSDK3487`: $..blob_sas_url
        - `AZSDK3488`: $..targetResourceRegion
        - `AZSDK3489`: $..domain_name
        - `AZSDK3490`: $..etag
        - `AZSDK3491`: $..functionUri
        - `AZSDK3492`: $..secondaryConnectionString
        - `AZSDK3493`: $..name
        - `AZSDK3494`: $..friendlyName
        - `AZSDK3495`: $..targetModelLocation
        - `AZSDK3496`: $..resourceLocation
        - `AZSDK3497`: $..keyVaultClientId (replaces with EMPTYGUID)
        - `AZSDK3498`: $..storageAccountAccessKey

        ### URI Regex Sanitizers (4XXX series)

        - `AZSDK4000`: SAS signatures in URIs (sig= parameter)
        - `AZSDK4001`: Host names in URIs (replaces with fake hosts)

        ### Remove Header Sanitizers (4XXX series)

        - `AZSDK4003`: Removes Telemetry-Source-Time header completely
        - `AZSDK4004`: Removes Message-Id header completely
    """;

    private readonly Option<bool> skipInitialRunOpt = new(["--skip-initial-run"], () => false, "Skip the initial sanity-check run of the live tests");
    private readonly Option<string> packageDirectoryOpt = new(["--package-directory", "-p"], () => Environment.CurrentDirectory, "Package directory to record tests for. Defaults to the current working directory");

    public override Command GetCommand()
    {
        var command = new Command("record-tests", "record tests agentically")
        {
            packageDirectoryOpt,
            skipInitialRunOpt
        };

        command.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
        return command;
    }

    private class TestFailure
    {
        [Description("The name of the test that failed")]
        public string TestName { get; set; }

        [Description("The full error message, verbatim, from the test run")]
        public string ErrorMessage { get; set; }

        [Description("A one-sentence human-readable summary of the failure reason")]
        public string HumanReadableError { get; set; }
    }

    public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct)
    {
        var packageDirectory = ctx.ParseResult.GetValueForOption(this.packageDirectoryOpt);
        var skipInitialRun = ctx.ParseResult.GetValueForOption(this.skipInitialRunOpt);
        var result = await RecordTests(packageDirectory, skipInitialRun, ct);
        output.Output(result);
        if (!result.Success)
        {
            SetFailure();
        }
    }

    [McpServerTool(Name = "azsdk_package_record_tests")]
    [Description("Records tests for a package and provides suggestions to fix test failures")]
    public async Task<RecordTestsResponse> RecordTests(
        string packageDirectory,
        bool skipInitialRun = false,
        CancellationToken ct = default
    )
    {
        try
        {
            var testRunner = await testRunnerResolver.GetTestRunnerAsync(packageDirectory);

            if (testRunner is null)
            {
                SetFailure();
                return new RecordTestsResponse(success: false, recommendation: $"No test runner service is available for package at {packageDirectory}.");
            }

            if (!skipInitialRun)
            {
                output.OutputConsole("Running live tests");
                var liveTestResults = await testRunner.RunAllTests(packageDirectory, TestMode.Live, ct);
                if (!liveTestResults.IsSuccessful)
                {
                    SetFailure();
                    return new RecordTestsResponse(success: false, recommendation: $"Live tests are failing. Fix live test failures before attempting to record tests.");
                }
            }

            var sanitizerConfigCode = await agentHost.RunAgentToCompletion(new Microagent<string>
            {
                Instructions = $"Find where the sanitizer options are set up in these tests and the environment variable playback setup and output the code",
                Tools = [
                    new ListFilesTool(Path.Join(Environment.CurrentDirectory, "test")),
                new ReadFileTool(Path.Join(Environment.CurrentDirectory, "test")),
            ]
            }, ct);

            var recordResult = await RunRecordMode(testRunner, packageDirectory, sanitizerConfigCode, ct);
            if (!recordResult.Success)
            {
                return recordResult;
            }

            return await RunPlaybackMode(testRunner, packageDirectory, sanitizerConfigCode, ct);
        }
        catch (Exception e)
        {
            SetFailure();
            return new RecordTestsResponse(success: false)
            {
                ResponseError = e.ToString()
            };
        }
    }

    private async Task<RecordTestsResponse> RunPlaybackMode(ITestRunner testRunner, string packageDirectory, string sanitizerConfigCode, CancellationToken ct)
    {
        logger.LogInformation("Running tests in playback mode");
        var playbackTestResults = await testRunner.RunAllTests(packageDirectory, TestMode.Playback, ct);

        if (playbackTestResults.IsSuccessful)
        {
            return new RecordTestsResponse(success: true);
        }

        // Look one failure at time. Often, fixing one failure will fix other failures too.
        var testFailure = playbackTestResults.Failures[0];
        logger.LogInformation("Attempting to fix failure: {TestName}; failure reason: {FailureReason}", testFailure.TestIdentifier, testFailure.FailureDetails);

        logger.LogInformation("Extracting test implementation");
        var implementation = await testRunner.GetTestImplementation(packageDirectory, testFailure.TestIdentifier, ct);

        var assetsDir = Path.Join(gitHelper.DiscoverRepoRoot(Environment.CurrentDirectory), ".assets");
        var recordingJsonFiles = Directory.GetFileSystemEntries(assetsDir, "*.json", SearchOption.AllDirectories);

        // Use AI here since the mapping varies greatly by language
        var recordingFilePath = await agentHost.RunAgentToCompletion(new Microagent<string>
        {
            Instructions = $"""
            Out of these recording file paths, identify the file path that corresponds to the test named {testFailure.TestIdentifier}:

            {string.Join(Environment.NewLine, recordingJsonFiles)}
            """
        }, ct);

        var recordingFileContent = await File.ReadAllTextAsync(recordingFilePath, ct);

        // TODO: provide tools to give the agent context e.g. let it look up the source code, other recordings, see sanitizer implementation, etc.
        var recommendation = await agentHost.RunAgentToCompletion(new Microagent<string>
        {
            Instructions = $"""
            The below test case and test implementation is passing in record and live modes, but once the recording is used in playback mode, the test fails. This implies that the implementation is correct,
            since otherwise the tests would fail in live mode. Thus, it is likely that the issue is with the recording or with the sanitization configuration.

            As a testing expert, suggest potential fixes to the test or sanitization configuration that may resolve the issue. Issues may include:
            - Over-sanitization: test recordings are sanitized, and sometimes over-sanitization may lead to values in one request/response not matching subsequent request/responses.
                This could be due to over-sanitization, or due to the application of global sanitizers, which must be suppressed. You can tell if a global sanitizer has been applied if you see the value
                "Sanitized" in the recording. Global sanitizers being present do not necessarily present a problem, but if you are seeing things like parsing errors, continuity errors e.g. IDs not matching between requests,
                or authentication errors, then it is likely that a global sanitizer is the culprit.
            - Missing recording: if the error is something along the lines of "cannot find request", then it is likely the test was not recorded properly. Re-recording should fix the issue in most cases.
            There may be other issues at play.

            Output any necessary code changes in diff format.

            {GlobalSanitizerReference}

            <TestFailureMessage>
            {testFailure.FailureDetails}
            </TestFailureMessage>

            <TestImplementation>
            {implementation}
            </TestImplementation>

            <SanitizerConfiguration>
            {sanitizerConfigCode}
            </SanitizerConfiguration>

            <RecordingFileContent>
            {recordingFileContent}
            </RecordingFileContent>
            """
        });

        return new RecordTestsResponse(success: false, failedTestName: testFailure.TestIdentifier, recommendation: recommendation);
    }

    private async Task<RecordTestsResponse> RunRecordMode(ITestRunner testRunner, string packageDirectory, string sanitizerConfiguration, CancellationToken ct)
    {
        output.OutputConsole("Running tests in record mode");
        var recordedTestResults = await testRunner.RunAllTests(packageDirectory, TestMode.Record, ct);

        if (recordedTestResults.IsSuccessful)
        {
            output.OutputConsole("Tests passed in record mode. Recording successful.");
            return new RecordTestsResponse(success: true);
        }

        var testResults = await testRunner.RunAllTests(packageDirectory, TestMode.Record, ct);

        if (testResults.Failures.Count == 0)
        {
            output.Output("No failures reported, but exit code was non-zero. Investigate manually.");
            return new RecordTestsResponse(success: false, recommendation: "No failures reported, but exit code was non-zero. Investigate manually.");
        }

        var testFailure = testResults.Failures.First();
        logger.LogInformation("Attempting to fix failure: {TestName}; failure reason: {FailureReason}", testFailure.TestIdentifier, testFailure.FailureDetails);

        logger.LogInformation("Extracting test implementation");
        var implementation = await testRunner.GetTestImplementation(packageDirectory, testFailure.TestIdentifier, ct);

        var recommendation = await agentHost.RunAgentToCompletion(new Microagent<string>
        {
            Instructions = $"""
            The below test case and test implementation is passing in live mode, but once we begin recording the test with the test proxy, it starts failing. This implies that the implementation is correct,
            since otherwise the tests would fail in live mode.

            As a testing expert, suggest potential fixes to the test or sanitization configuration that may resolve the issue. Issues may include:
            - Over-sanitization: test recordings are sanitized, and sometimes over-sanitization may lead to values in one request/response not matching subsequent request/responses.
                This could be due to over-sanitization, or due to the application of global sanitizers, which must be suppressed. You can tell if a global sanitizer has been applied if you see the value
                "Sanitized" in the recording. Global sanitizers being present do not necessarily present a problem, but if you are seeing things like parsing errors, continuity errors e.g. IDs not matching between requests,
                or authentication errors, then it is likely that a global sanitizer is the culprit.
            - Recorder setup issues.

            <TestFailureMessage>
            {testFailure.FailureDetails}
            </TestFailureMessage>

            <TestImplementation>
            {implementation}
            </TestImplementation>

            <SanitizerConfiguration>
            {sanitizerConfiguration}
            </SanitizerConfiguration>
            """
        }, ct);

        return new RecordTestsResponse(success: false, failedTestName: testFailure.TestIdentifier, recommendation: recommendation);
    }
}