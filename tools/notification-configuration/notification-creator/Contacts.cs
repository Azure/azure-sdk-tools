using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Azure.Sdk.Tools.CodeOwnersParser;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Build.WebApi;

namespace Azure.Sdk.Tools.NotificationConfiguration;

/// <summary>
/// This class represents a set of contacts obtained from CODEOWNERS file
/// located in repository attached to given build definition [1].
///
/// The contacts are the CODEOWNERS path owners of path that matches the build definition file path.
///
/// To obtain the contacts, construct this class and then call GetFromBuildDefinitionRepoCodeowners(buildDefinition).
///
/// [1] https://learn.microsoft.com/en-us/rest/api/azure/devops/build/definitions/get?view=azure-devops-rest-7.0#builddefinition
/// </summary>
internal class Contacts
{
    private readonly ILogger log;
    private readonly GitHubService gitHubService;

    // Type 2 maps to a build definition YAML file in the repository.
    // You can confirm it by decompiling Microsoft.TeamFoundation.Build.WebApi.YamlProcess..ctor.
    private const int BuildDefinitionYamlProcessType = 2;

    internal Contacts(GitHubService gitHubService, ILogger log)
    {
        this.log = log;
        this.gitHubService = gitHubService;
    }

    /// <summary>
    /// See the class comment.
    /// </summary>
    public async Task<List<string>> GetFromBuildDefinitionRepoCodeowners(BuildDefinition buildDefinition)
    {
        if (buildDefinition.Process.Type != BuildDefinitionYamlProcessType)
        {
            this.log.LogDebug(
                "buildDefinition.Process.Type: '{buildDefinitionProcessType}' " + 
                "for buildDefinition.Name: '{buildDefinitionName}' " +
                "must be '{BuildDefinitionYamlProcessType}'.",
                buildDefinition.Process.Type,
                buildDefinition.Name,
                BuildDefinitionYamlProcessType);
            return null;
        }
        YamlProcess yamlProcess = (YamlProcess)buildDefinition.Process;

        Uri repoUrl = GetCodeownersRepoUrl(buildDefinition);
        if (repoUrl == null)
        {
            // assert: the reason why repoUrl is null has been already logged.
            return null;
        }

        List<CodeownersEntry> codeownersEntries = await gitHubService.GetCodeownersFileEntries(repoUrl);
        if (codeownersEntries == null)
        {
            this.log.LogInformation("CODEOWNERS file in '{repoUrl}' not found. Skipping sync.", repoUrl);
            return null;
        }

        // yamlProcess.YamlFilename is misleading here. It is actually a file path, not file name.
        // E.g. it is "sdk/foo_service/ci.yml".
        string buildDefinitionFilePath = yamlProcess.YamlFilename; 

        this.log.LogInformation(
            "Searching CODEOWNERS for matching path for '{buildDefinitionFilePath}'",
            buildDefinitionFilePath);

        CodeownersEntry matchingCodeownersEntry = GetMatchingCodeownersEntry(
            yamlProcess,
            codeownersEntries,
            repoUrl.ToString());
        List<string> contacts = matchingCodeownersEntry.Owners;

        this.log.LogInformation(
            "Found matching contacts (owners) in CODEOWNERS. " +
            "Searched path '{buildDefinitionOwnerFile}', Contacts#: {contactsCount}",
            buildDefinitionFilePath,
            contacts.Count);

        return contacts;
    }

    private Uri GetCodeownersRepoUrl(BuildDefinition buildDefinition)
    {
        Uri repoUrl = buildDefinition.Repository.Url;
        this.log.LogInformation("Fetching CODEOWNERS file from repoUrl: '{repoUrl}'", repoUrl);

        if (!string.IsNullOrEmpty(repoUrl?.ToString()))
        {
            repoUrl = new Uri(Regex.Replace(repoUrl.ToString(), @"\.git$", string.Empty));
        }
        else
        {
            this.log.LogError(
                "No repository url returned from buildDefinition. " +
                "buildDefinition.Name: '{buildDefinitionName}' " +
                "buildDefinition.Repository.Id: {buildDefinitionRepositoryId}",
                buildDefinition.Name,
                buildDefinition.Repository.Id);
        }

        return repoUrl;
    }

    private CodeownersEntry GetMatchingCodeownersEntry(
        YamlProcess process,
        List<CodeownersEntry> codeownersEntries,
        string repoUrl)
    {
        CodeownersEntry matchingCodeownersEntry =
            CodeownersFile.GetMatchingCodeownersEntry(
                process.YamlFilename,
                codeownersEntries,
                UseRegexMatcher(repoUrl));

        matchingCodeownersEntry.ExcludeNonUserAliases();

        return matchingCodeownersEntry;
    }

    /// <summary>
    /// Whether the new regex-based CODEOWNERS matcher that supports wildcards should be used
    /// for given repository. This method exists to allow incremental roll-out
    /// of the new matcher [1].
    ///
    /// Note that enabling the new matcher will not change the reviewers assigned to
    /// PRs, because this is handled by built-in GitHub matcher. 
    /// But it will change who receives build failure notifications.
    ///
    /// [1] https://github.com/Azure/azure-sdk-tools/pull/5088
    /// </summary>
    private bool UseRegexMatcher(string repoUrl)
    {
        // Expected repoUrl: https://github.com/Azure/azure-sdk-for-net
        if (repoUrl.Contains("azure-sdk-for-net"))
        {
            // Work that was done to facilitate enabling the matcher in this repo:
            // https://github.com/Azure/azure-sdk-for-net/pull/33584 - Fix invalid paths in CODEOWNERS
            // https://github.com/Azure/azure-sdk-for-net/pull/33595 - Remove CODEOWNERS rules /**/ci.yml and /**/tests.yml
            // https://github.com/Azure/azure-sdk-for-net/pull/33597 - Add missing /sdk/<dir> rules
            return true;
        }

        return false;
    }
}
