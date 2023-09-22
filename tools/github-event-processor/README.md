# GitHub Event Processor

## Overview

GitHub Event Processor is written in C# using [Octokit.Net](https://github.com/octokit/octokit.net). GitHub Event Processor will utilize GitHub Actions and Scheduled events, triggered through [GitHub Action Workflows](https://docs.github.com/en/actions/using-workflows/about-workflows). These are defined in YML files and placed into the .github/workflows directory of the repository utilizing them. For our purposes there will be two YML files, one for Actions and one for Scheduled events.

[Rules and Cron task definitions](./RULES.md)

## Events, Actions and YML

GitHubEventProcessor is invoked by the event-processor.yml file that lives in .github/workflow directory. The directory is special, GitHub will automatically process any yml file in this directory as a GitHub actions file. This yml file defines which events, and which actions on those events, we wish to process. *The full list events and their corresponding actions can be found [here](https://docs.github.com/en/actions/using-workflows/events-that-trigger-workflows)*

For example:

```yml
on:
  # issues is the event
  issues:
    # these are the issues actions we're interested in processing, other issues
    # actions will not trigger processing from this yml file.
    types: [edited, labeled, opened, reopened, unlabeled]
  # issue_comment (includes PullRequest comments)
  issue_comment:
    # for comments, we only care to process when they're created
    types: [created]
```

This means that GitHub will only invoke the job in the yml file when an **issue** is edited, labeled, opened, reopened and unlabeled or an **issue_comment** is created. All other events, and their actions, that aren't defined in the yml file will not trigger any processing.

### Command Line Arguments

If running an action:

```powershell
github-event-processor ${{ github.event_name }} payload.json
```

If running a scheduled task:

```powershell
github-event-processor ${{ github.event_name }} payload.json <TaskToRun>
```

**github.event_name** will be one of the [workflow trigger events](https://docs.github.com/en/actions/using-workflows/events-that-trigger-workflows). These are things like issues, issue_comment, pull_request_target, pull_request_review etc. This option is how the application knows what to class to deserialize the event payload into.

**payload.json** is the toJson of the github.event redirected into file. The action that triggered the event is part of this payload.

**TaskToRun** is specific to Scheduled event processing and defines what rule to run. This string matches the rule name constant defined in the [RulesConstants](./Azure.Sdk.Tools.GitHubEventProcessor/Constants/RulesConstants.cs) file. The reason this was done this way is that it prevents the code from needing knowledge of which cron schedule string belongs to which rule.

### Rules Configuration

The [rules configuration file](./YmlAndConfigFiles/event-processor.config) is simply a Json file which defines which rules are active for the repository and they're loaded up every time the GitHubEventProcessor runs. The full set rules is in the [RulesConstants](./Azure.Sdk.Tools.GitHubEventProcessor/Constants/RulesConstants.cs) file and their state is either **On** or **Off**. *Note: AzureSdk language repositories should have all rules enabled but non-language repositories, like azure-sdk-tools, have a reduced set of rules. For example:

```json
  "InitialIssueTriage": "On",
  "ManualIssueTriage": "On",
  "ServiceAttention": "Off",
```

All three of the above rules are *Issues* event rules. InitialIssueTriage and ManualIssueTriage would both run because they're **On** but ServiceAttention would not because it's **Off**. Also, just because a rule is **On** doesn't mean it'll always make updates to an Issue.

Every rule has a the following definition:

- **Trigger** - The event and action that will cause a given rule to process.
- **Criteria** - A set of evaluations performed for a given trigger that will determine what, if any, action to take.
- **Actions** - A set of things to happen in response to a trigger based upon the criteria.

For example, **ManualIssueTriage** is a rule that will only processes on an **Issues** event's **labeled** action. Its criteria is that the issue must be *Open*, have the *needs-triage* label and the label being added is not *needs-triage*. The action to take, if all the criteria has been met, is to remove the *needs-triage* label from the issue.

The full list of Rules and their definitions can be found [here](./RULES.md)

### GitHub Authentication in Actions

GitHub provides a token secret, GITHUB_TOKEN, which can be used to authenticate on behalf of GitHub Actions. Full documentation can be found [here](https://docs.github.com/en/actions/security-guides/automatic-token-authentication).

#### GITHUB_TOKEN

At the start of each action workflow, GitHub automatically creates a unique GITHUB_TOKEN secret which is used to authenticate during processing for that action. The token expires at the end of the action processing or 24 hours, whichever happens first.

#### A note about the GITHUB_TOKEN used in Actions processing

Actions are caused by activities defined on events. For example *issues* is an event and *labeled* would an action on that event. Changes made to issues, pull requests, comments etc, using the GITHUB_TOKEN do not cause other actions to fire. For example, **Manual Issue Triage** can remove a label if the criteria for the rule has been met but this removing this label will not cause an *issues* *unlabeled* event to fire. This is intentional to prevent accidental creation of circular workflow runs.

### Actions vs Scheduled Events

Actions, as previously stated, are triggered by activities on events. These are limited in scope to a single *Issue* or *Pull Request* and process immediately.

Scheduled Events are cron tasks which perform operations on multiple *Issues* or *Pull Requests* as determined by queries different to each task. For example, the **Close Stale Issues** event looks for all *Issues* that have both *needs-author-feedback* and *no-recent-activity* labels which haven't been modified for 14 days and closes them.

### GitHub Event Processor has the following dependencies

1. Octokit.Net - The json payloads are deserialized using Octokit's SimpleJsonSerializer into Octokit classes and all communications with GitHub are done through Octokit's GitHubClient.
2. CodeOwnersParser - CodeOwners Parser is used for the following but, in the near future, will be modified to handle a special case in the Initial Issue Triage rule.
    - Retrieving auto labels for Pull Request based upon the file paths in the Pull Request
    - Getting the list of people to @ mention when Service Attention is added to an Issue

In addition to the above dependencies, the GitHubEventProcessor.Test project has the following additional test dependencies.

1. NUnit - NUnit unit testing
2. NUnit3TestAdapter - Used to run NUnit3 tests in Visual Studio
3. Microsoft.NET.Test.Sdk - For the ability to run tests using **dotnet test**
4. coverlet.collector - Generates test coverage data

Note: github-event-processor's project has PackAsTool set to true which builds the project, with its dependencies into a single package. This will be installed as part of the GitHub Action yml.

## Where to install from?

The application is built and published to the [azure-sdk-for-net dev feed](https://dev.azure.com/azure-sdk/public/_artifacts/feed/azure-sdk-for-net/NuGet/Azure.Sdk.Tools.GitHubEventProcessor).
