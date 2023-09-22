# GitHub Event Processor Breakdown

This is going to be at a very high level as specifics can be found within the code.

## GitHub Event Client

[GitHubEventClient](./GitHubEventClient.cs) is a singleton and contains the following:

1. The authenticated GitHubClient - Authenticated using the GITHUB_TOKEN provided by GitHub Actions.
2. The loaded Rules Configuration for the repository it's running in
3. Convenience functions - Anything having to deal with GitHub communication/queries and Rules.
4. All pending updates - Event processing can create a number of different updates, things like issue updates, comments, review dismissals etc. During event processing, any updates are stored here and when all the rules have been processed the pending updates will be made.

### Why pending updates are handled at the end of processing

GitHub mostly treats Issues and Pull Requests the same. Title, Body, Assignees, Milestone, State (Open/Closed) and Labels are all modified and updated in the same manner. With the GitHub API, any or all of the aforementioned items are updated with a single call regardless of the number of changes. Any rules that run as a result of processing for a specific Event/Action will make any of these types of changes to a shared IssueUpdate instance and the resulting update will be the culmination of changes. This differs from how FabricBot processes rules today in which each rule operates on the original item and simultaneous updates are made for each rule. This has the possibility to do make conflicting updates in which the last one in wins.

## Constants

[Constants](./Constants/)

- **Event Constants** - These match the [events that trigger workflows](https://docs.github.com/en/actions/using-workflows/events-that-trigger-workflows). These are used to determine what payload class to deserialize the json event payload into and what type of event is being processed. For example, **issues** and **issue_comment**
- **Action Constants** - Each event has a number of activities it supports and these is used in rules processing. Every action rule has criteria, defining what action on a give event should cause processing. For example, **labeled** is action on an **issue**.
- **Label Constants** - These are very specifically labels that are common across all of the repositories and used explicitly by rules processing. These are labels like "needs-triage" or "Service Attention". No team or language specific labels belong in here.
- **Rules Constants** - These match the rules the [event-processor.config](../YmlAndConfigFiles/event-processor.config) file. If a new rule is being created, then a new constant needs to be defined here and will ultimately need to be added to the config file so it'll process if turned on.
- **Comment Constants** - There are rules that process based upon certain phrases being in comments. For example, Reopen Pull Request triggers on the issue_comment creation where the comment has the word **/reopen** in it.
- **Org Constants** - This is actually Org and Product constants. It just contains the org constant for Azure and the constant for the product header name which is required when registering the GitHubClient.

## GitHubPayload

[GitHubPayload](./GitHubPayload/) For the most part, the **GITHUB_PAYLOAD** from the action maps exactly to the Octokit.Net classes with a couple of exceptions. The payload for Pull Request and Issue Events, specifically for labeled/unlabeled actions, contains the label that was being added or removed which the Octokit.Net classes did not have. The IssueEventGitHubPayload and PullRequestEventGitHubPayload classes are effectively the same as their Octokit.Net counterparts with the exception of this added label. PullRequestEventGitHubPayload was also missing AutoMergeEnabled. Since we don't need the contents of that, just whether it's there or not, this was simply replaced with a flag. The other exception was ScheduledEventGitHubPayload. Octokit.Net isn't explicitly made for GitHub Actions and doesn't have a Scheduled event which is why this class was necessary.

## Event Processing

[EventProcessing](./EventProcessing/) The files are all **Event**Processing.cs and contain all of the processing rules for that event type. For example, [IssueProcessing.cs](./EventProcessing/IssueProcessing.cs) contains all of the rules for Issue Processing. The rules are defined [here](../RULES.md).

### Event Actions vs Scheduled Events

Event Actions are user driven, someone creating an issue or adding a label a pull request. Scheduled Events are cron driven. Event Actions get most of their information from the **GITHUB_PAYLOAD** when determining whether or not to process. Scheduled Events execute a search query and all of the criteria is part of the query. Event Actions process on a single, usually an Issue or Pull Request whereas Scheduled Events will process multiple Issues or Pull Requests returned from the query.

### Event Action Processing

1. Check whether or not the rule is **On** or **Off**, if **Off** then the rule will not process.
2. Check the trigger (otherwise known as Action). For example, Manual Issue Triage only processes when the **Issues** action is **Labeled**
3. Check the criteria. For example, Manual Issue Triage will only process if the Issue is Open, has the label *needs-triage* and the label being added isn't *needs-triage*.
4. If 1-3 are true, perform the actions for that rule. For example, Manual Issue Triage, if 1-3 are true then the *needs-triage* label is removed from the Issue.

#### An important note about Event Action criteria

Most of the criteria information comes from the **GITHUB_PAYLOAD** but there are several rules that require a call to GitHub. For example, some rules require knowing whether or not the user that initiated the action has certain permissions on the repository. Because calls to GitHub count against the Core rate limit for the repository, those are the absolute last piece of criteria checked, only after the rest of the criteria is true. This is purposely done to minimize the number of calls that count against the rate limit.

### Scheduled Event Processing

1. Check whether or not the rule is **On** or **Off**, if **Off** then the rule will not process.
2. The criteria for Scheduled Event is the query information to search call. For example, Close Addressed Issues will query for issues that are Open, have the label *issue-addressed* and have had no activity for at least 7 days.
3. All items returned from the query *can* be processed.

#### Important notes about Scheduled Event Processing

- For any given Scheduled Event, the processing should be such that, after processing, if the query was immediately run a second time, anything that was just processed would not show up again. For example, part of the criteria for the Close Addressed Issues search is that the issue is open. Processing for this rule closes the issue, which would no longer cause it to show up in query.
- Scheduled Events use the same Core rate limit as Event Action processing. The number of items a given Scheduled Event process is currently governed to 1/10th of the hourly Core rate limit. This is to prevent Scheduled Event processing from eating up the rate limit and adversely affecting Event Action processing.

## Utilities

[Utilities](./Utilities/)

- **DirectoryUtils** - Used to ascend to the root of the repository and, given a subdirectory hint, find a given file. CODOWNERS and the rule configuration both use this to discover the location of their respective files to load.
- **CodeOwnersUtils** - This contains convenience functions to load the CODEOWNERS file and perform the following actions:
  - Getting the list of labels based upon file paths for pull_requests
  - Getting the list of people to @ mention for Service Attention
- **CommentUtils** - There are several rules that scan comments for specific things, this just contains the common search function to ensure they're all looking for things in the same manner regardless of the rule.
- **LabelUtils** - Similar to comment utils, it contains a common function that'll accept a list of labels from an Issue or Pull Request and check to see if a given label exists.
- **RulesConfiguration** - Methods for loading the rules configuration, checking whether a given rule is enabled, reporting missing rules and a method, used by the mock, to create a default configuration used for testing.
