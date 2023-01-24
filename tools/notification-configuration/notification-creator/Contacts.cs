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
/// located in repository attached to given build definition.
///
/// The contacts are the CODEOWNERS path owners of path that matches the build definition file path.
///
/// To obtain the contacts, construct this class and then call GetFromBuildDefinitionRepoCodeowners(buildDefinition).
/// </summary>
internal class Contacts
{
    private readonly ILogger log;
    private readonly GitHubService gitHubService;

    // Type 2 maps to a build definition YAML file in the repository
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
                "buildDefinition.Process.Type for buildDefinition.Name = '{buildDefinitionName}' " +
                "is not BuildDefinitionYamlProcessType = {BuildDefinitionYamlProcessType}.",
                buildDefinition.Name,
                BuildDefinitionYamlProcessType);
            return null;
        }

        Uri repoUrl = GetCodeownersRepoUrl(buildDefinition);

        if (repoUrl == null)
        {
            // assert: the reason why repoUrl is null has been already logged.
            return null;
        }

        List<CodeownersEntry> codeownersEntries = await gitHubService.GetCodeownersFileEntries(repoUrl);

        if (codeownersEntries == default)
        {
            this.log.LogInformation("CODEOWNERS file in '{repoUrl}' not found, skipping sync.", repoUrl);
            return null;
        }

        if (buildDefinition.Process is not YamlProcess process)
        {
            this.log.LogError(
                "buildDefinition.Process as YamlProcess is null. buildDefinition.Name: '{buildDefinitionName}'",
                buildDefinition.Name);
            return null;
        }

        // process.YamlFilename is misleading here. It is actually a file path, not file name.
        // E.g. it is "sdk/foo_service/ci.yml".
        string buildDefinitionFilePath = process.YamlFilename; 

        this.log.LogInformation(
            "Searching CODEOWNERS for matching path for '{buildDefinitionFilePath}'",
            buildDefinitionFilePath);

        CodeownersEntry matchingCodeownersEntry = GetMatchingCodeownersEntry(process, codeownersEntries);
        List<string> contacts = matchingCodeownersEntry.Owners;

        this.log.LogInformation(
            "Found matching contacts (owners) in CODEOWNERS. " +
            "Searched path = '{buildDefinitionOwnerFile}', Contacts# = {contactsCount}",
            buildDefinitionFilePath,
            contacts.Count);

        return contacts;
    }

    private Uri GetCodeownersRepoUrl(BuildDefinition buildDefinition)
    {
        Uri repoUrl = buildDefinition.Repository.Url;
        this.log.LogInformation("Fetching CODEOWNERS file from repoUrl: '{repoUrl}'", repoUrl);

        if (repoUrl != null)
        {
            repoUrl = new Uri(Regex.Replace(repoUrl.ToString(), @"\.git$", String.Empty));
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


    private CodeownersEntry GetMatchingCodeownersEntry(YamlProcess process, List<CodeownersEntry> codeownersEntries)
    {
        CodeownersEntry matchingCodeownersEntry =
            CodeownersFile.GetMatchingCodeownersEntry(process.YamlFilename, codeownersEntries);

        matchingCodeownersEntry.ExcludeNonUserAliases();

        return matchingCodeownersEntry;
    }
}
