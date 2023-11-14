# Azure SDK Repository Automation Rules

- [Overview](#overview)
- [Issue Rules](#issue-rules)
- [Pull Request Rules](#pull-request-rules)
- [Scheduled rules](#scheduled-rules)

# Overview

## Nomenclature

- **Trigger**: An event that occurs in GitHub to which automation should respond to.
- **Target**: An item in GitHub that can trigger an event.  Generally, this will be an issue or pull request.
- **Criteria**: A set of evaluations performed for a trigger target that determines which actions, if any, result.
- **Actions**: A set of "things that happen" in response to a trigger. For example, adding a label to an issue.

## Data integrations

- **Service partners**: Each partner team that owns packages in our repository will need to have an association tracking a service label to one or more GitHub handles.  The contacts for service teams will follow the same pattern as the "Service Attention" process does today; it must be possible to assign a set of service owners that differs from the individuals assigned as pull request reviewers. If it exists, this information is in the repository's CODEOWNERS file.

  Example:

```text
# ServiceLabel: %Event Grid %Service Attention
/sdk/eventgrid/                                                    @user1 @user2 @user3 @user4
```

- **Azure SDK team owners**: Each package in our repository that is owned by our team will need to have an association tracking a service label and category label pair to one or more GitHub handles.  This is an unofficial mapping that does not necessarily correlate to authoritative "Azure SDK Team" membership, such as that represented by our security groups or the "[azure-sdk-team](https://repos.opensource.microsoft.com/orgs/Azure/teams/azure-sdk-team)" GitHub team.  That said, using one of these groups to validate membership on top of the label association would be just fine. *Right now, this does not exist, this would require changes to CODEOWNERS which are still pending.* If it exists, the Service Label to user mapping is in the repository's CODEOWNERS file.

  Example:

```text
# ServiceLabel: %Event Hubs
/sdk/eventhub/                                                     @user1 @user2 @user3
```

- **File paths**: A service label may have one or more file paths relative to the repository structure associated with it. If it exists, this information is in the repository's CODEOWNERS file today.

  Example:

```text
# # PRLabel: %Attestation
/sdk/attestation/                                                  @user1 @user2
/sdk/attestation/azure-security-attestation                        @user3 @Azure/fake-team-name
```

## External integrations

### AI label service

This is a REST API that evaluates the content of an issue and attempts to predict a set of labels that should be applied to it.  If a prediction is made with a reasonable level of confidence, the service will return exactly one service label and one category label.  If confidence thresholds were not met for both label types, no labels are returned. The AI label service can only a response with labels for [officially supported repositories]
(<https://github.com/Azure/azure-sdk-tools/blob/main/tools/github-labels/repositories.txt>). For libraries that are not supported it an empty response will be returned.

#### Example payload

```json
{
  "issueNumber": 32227,
  "title": "Move Snippet Generator to .NET 6",
  "body": "The snippet generator tool currently targets netcoreapp 3.1 which reaches end-of-life in December, 2022. The target framework should be updated to net6.0 and the associated Update-Snippet script should be updated to reference the new version.",
  "issueUserLogin": "person1",
  "repositoryName": "azure-sdk-for-net",
  "repositoryOwnerName": "Azure"
}
```

#### Example responses

_**Predictions made**_

```json
{
  "labels": [
    "Storage",
    "Client"
  ]
}
```

_**No predictions**_

```json
{
  "labels": []
}
```

### Important context

This is a stand-alone service providing a REST API which requires a service key to access. The service key is pulled pulled from KeyVault and stored as a secret in the action's environment.

# Issue Rules

## Initial issue triage

### Trigger

- Issue created

### Criteria

- Issue has no labels
- Issue has no assignee

### Actions

- Query AI label service for suggestions:

```text
IF labels were predicted:
    - Assign returned labels to the issue

    IF service and category labels have AzureSdkOwners (in CODEOWNERS):
        IF a single AzureSdkOwner:
            - Assign the AzureSdkOwner issue
        ELSE
            - Assign a random AzureSdkOwner from the set to the issue
            - Create the following comment, mentioning all AzureSdkOwners from the set
                 "@{person1} @{person2}...${personX}"

        - Create the following comment
             "Thank you for your feedback.  Tagging and routing to the team member best able to assist."

    # Note: No valid AzureSdkOwners means there were no CODEOWNERS entries for the service label OR no
    # CODEOWNERS entries for the service label with AzureSdkOwners OR there is a CODEOWNERS entry with
    # AzureSdkOwners but none of them have permissions to be assigned to an issue for the repository.
    IF there are no valid AzureSdkOwners, but there are ServiceOwners, and the ServiceAttention rule is enabled
    for the repository
        - Add "Service Attention" label to the issue and apply the logic from the "Service Attention" rule
    ELSE
        - Add "needs-team-triage" (at this point it owners cannot be determined for this issue)

    IF "needs-team-triage" is not being added to the issue
         - Add "needs-team-attention" label to the issue

ELSE
    - Add "needs-triage" label to the issue

```

- Evaluate the user that created the issue:

```text
IF the user is NOT a member of the Azure Org
  IF the user does not have Admin or Write Collaborator permission
        - Add "customer-reported" label to the issue
        - Add "question" label to the issue
```

Note: Users are supposed to be **public** members of Azure. This is very clearly stated in the
the [onboarding docs](https://eng.ms/docs/products/azure-developer-experience/onboard/access). If a user's
Azure membership is private, API call to check Azure membership will return false which can result in "customer-reported" and "question" labels being added to an issue for someone that is a private member of Azure.

## Manual issue triage

### Trigger

- Issue modified for:
  - Label added

### Criteria

- Issue is open
- Issue has "needs-triage" label
- Label being added is NOT "needs-triage"

### Actions

- Remove "needs-triage" label

## Service Attention

### Trigger

- Issue modified for:
  - Label added

#### Criteria

- Issue is open
- Label added is "Service Attention"
- One or more service team contacts is associated with the set of labels assigned to the issue

### Actions

- Create the following comment, mentioning the service team contacts
  - "Thanks for the feedback! We are routing this to the appropriate team for follow-up. cc ${mentionees}."

## Manual triage after external assignment

### Trigger

- Issue modified for:
  - Label removed

### Criteria

- Issue is open
- Issue is not assigned
- Issue has "customer-reported" label
- Label removed is "Service Attention"

### Actions

- Add "needs-team-triage" label

## Author feedback

### Trigger

- Issue comment created

### Criteria

- Issue is open
- Issue has "needs-author-feedback" label
- Commenter is the original issue author

### Actions

- Remove "needs-author-feedback" label
- Add "needs-team-attention" label

## Reset issue activity

### Trigger

- Issue modified for:
  - Reopen
  - Content edited

OR

- Issue comment created

### Criteria

- Issue is open OR being reopened
- Issue has "no-recent-activity" label
- Account modifying the issue is NOT a known bot

### Actions

- Remove "no-recent-activity" label

## Reopen issue

### Trigger

- Issue comment created

### Criteria

- Issue is closed
- Issue has label "no-recent-activity"
- Issue has label "needs-author-feedback"
- Issue was closed for 7 days or less
- Commenter is the original issue author
- Action is not "comment and close"

### Actions

- Reopen the issue
- Remove "no-recent-activity" label
- Remove "needs-author-feedback" label
- Add "needs-team-attention" label

## Decline to reopen issue

### Trigger

- Issue comment created

### Criteria

- Issue is closed
- Issue was closed for more than 7 days ago
- Commenter does NOT have a collaborator association
- Commenter does NOT have write permission
- Commenter does NOT have admin permission
- Action is not "comment and close"

### Actions

- Create the following comment
  - "Thank you for your interest in this issue! Because it has been closed for a period of time, we strongly advise that you open a new issue linking to this to ensure better visibility of your comment."

## Require attention for non-milestone

### Trigger

- Issue modified for:
  - Label added
  - Label removed

### Criteria

- Issue is open
- Issue has label "customer-reported"
- Issue does NOT have label "needs-team-attention"
- Issue does NOT have label "needs-triage"
- Issue does NOT have label "needs-team-triage"
- Issue does NOT have label "needs-author-feedback"
- Issue does NOT have label "issue-addressed"
- Issue is not in a milestone

### Actions

- Add "needs-team-attention" label

## Author feedback needed

### Trigger

- Issue modified for:
  - Label added

### Criteria

- Issue is open
- Label added is "needs-author-feedback"

### Actions

- Remove "needs-triage" label
- Remove "needs-team-triage" label
- Remove "needs-team-attention" label
- Create the following comment
  - "Hi @{issueAuthor}. Thank you for opening this issue and giving us the opportunity to assist. To help our team better understand your issue and the details of your scenario please provide a response to the question asked above or the information requested above. This will help us more accurately address your issue."

## Issue Addressed

### Trigger

- Issue modified for:
  - Label added

### Criteria

- Issue is open
- Label added is "issue-addressed"

### Actions

- Remove "needs-triage" label
- Remove "needs-team-triage" label
- Remove "needs-team-attention" label
- Remove "needs-author-feedback" label
- Remove "no-recent-activity" label
- Create the following comment
  - "Hi @{issueAuthor}.  Thank you for opening this issue and giving us the opportunity to assist.  We believe that this has been addressed.  If you feel that further discussion is needed, please add a comment with the text “`/unresolve`” to remove the “issue-addressed” label and continue the conversation."

## Issue Addressed commands

### Trigger

- Issue comment created

### Criteria

- Issue has label "customer-reported"
- Comment text contains the string "/unresolve"

### Actions

- Evaluate the permissions of the commenter:

    ```text
    IF commenter is the issue author
      OR commenter has a collaborator association
      OR commenter has write permission
      OR commenter has admin permission:

          - Reopen the issue
          - Remove label "issue-addressed"
          - Add label "needs-team-attention"
    ELSE
        - Create the following comment
          - "Hi @{commenter}, only the original author of the issue can ask that it be unresolved.  Please open a new issue with your scenario and details if you would like to discuss this topic with the team."
    ```

## Issue Addressed reset

### Trigger

- Issue modified for:
  - Label added

### Criteria

- Issue is open
- Issue has label "issue-addressed"

- Label added is any one of:
  - "needs-team-attention"
  - "needs-author-feedback"
  - "Service Attention"
  - "needs-triage"
  - "needs-team-triage"

### Actions

- Remove "issue-addressed" label

# Pull Request Rules

## Pull Request triage

### Trigger

- PR created

### Criteria

- Pull request has no labels

### Actions

- Evaluate the paths for each file in the PR:

    ```text
    IF the path is associated with a label:
        - Assign the label
    ```

- Determine if this is a community contribution:

    ```text
    IF the user is NOT a member of the Azure Org
      IF the user does not have Admin or Write Collaborator permission
        - Add "customer-reported" label
        - Add "Community Contribution" label
        - Create the following comment
          - "Thank you for your contribution @{issueAuthor}! We will review the pull request and get back to you soon."
    ```

## Reset pull request activity

### Trigger

- PR modified for:
  - Reopen
  - Changes Pushed
  - Merged
  - Review Requested

OR

- Pull request comment created

### Criteria

- Pull request is open OR being reopened
- Pull request has "no-recent-activity" label
- User modifying the pull request is NOT a known bot

- If triggered by comment creation, evaluate:
  - Commenter is the pull request author OR has write permissions
  - Comment text does NOT contain the string "Check Enforcer"
  - Comment text does NOT contain the string "Since there hasn't been recent engagement, this is being closed out."

### Actions

- Remove "no-recent-activity" label

## Reopen pull request

### Trigger

- Pull request comment created

### Criteria

- Pull request is closed
- Pull request has "no-recent-activity" label
- Commenter is the pull request author OR has write or admin permission
- Comment text contains the string "/reopen"

### Actions

  ```text
  IF commenter is the pull request author
    OR the commenter has write permission
    OR the commenter has a collaborator association:
      - Remove "no-recent-activity" label
      - Reopen pull request

  ELSE
    - Create the following comment
      - "Sorry, @{commenter}, only the original author can reopen this pull request."
  ```

## Reset approvals for untrusted changes

### Trigger

- PR modified for:
  - Changes pushed (synchronize)

### Criteria

- Pull request is open
- auto-merge has been enabled through the GitHub UI on the pull request
- User who pushed the changes does NOT have a collaborator association
- User who pushed changes does NOT have write permission
- User who pushed changes does NOT have admin permission

### Actions

- Reset all approvals
- Create the following comment
  - "Hi @{issueAuthor}.  We've noticed that new changes have been pushed to this pull request.  Because it is set to automatically merge, we've reset the approvals to allow the opportunity to review the updates."

# Scheduled Rules

## Close stale issues

### Trigger

- CRON (daily at 1am)

### Criteria

- Issue is open
- Issue has "needs-author-feedback" label
- Issue has "no-recent-activity" label
- Issue was last modified more than 14 days ago

### Actions

- Close the issue

## Close stale pull requests

### Trigger

- CRON (every 6 hours)

### Criteria

- Pull request is open
- Pull request has "no-recent-activity" label
- Pull request was last modified more than 7 days ago

### Actions

- Close the pull request
- Create the following comment
  - "Hi @{issueAuthor}.  Thank you for your contribution.  Since there hasn't been recent engagement, we're going to close this out.  Feel free to respond with a comment containing "/reopen" if you'd like to continue working on these changes.  Please be sure to use the command to reopen or remove the "no-recent-activity" label; otherwise, this is likely to be closed again with the next cleanup pass."

## Identify stale issues

### Trigger

- CRON (every 6 hours)

### Criteria

- Issue is open
- Issue has "needs-author-feedback" label
- Issue does NOT have "no-recent-activity" label
- Issue was last updated more than 7 days ago

### Actions

- Add "no-recent-activity" label
- Create the following comment
  - "Hi, we're sending this friendly reminder because we haven't heard back from you in **7 days**. We need more information about this issue to help address it. Please be sure to give us your input. If we don't hear back from you within **14 days** of this comment the issue will be automatically closed. Thank you!"

## Identify stale pull requests

### Trigger

- CRON (weekly, Friday at 5am)

### Criteria

- Pull request is open
- Pull request does NOT have "no-recent-activity" label
- Pull request was last updated more than 60 days ago

### Actions

- Add "no-recent-activity" label
- Create the following comment
  - "Hi @{issueAuthor}.  Thank you for your interest in helping to improve the Azure SDK experience and for your contribution.  We've noticed that there hasn't been recent engagement on this pull request.  If this is still an active work stream, please let us know by pushing some changes or leaving a comment.  Otherwise, we'll close this out in 7 days."

## Close addressed issues

### Trigger

- CRON (every 6 hours)

### Criteria

- Issue is open
- Issue has label "issue-addressed"
- Issue was last updated more than 7 days ago

### Actions

- Close the issue
- Create the following comment
  - "Hi @{issueAuthor}, since you haven’t asked that we “`/unresolve`” the issue, we’ll close this out. If you believe further discussion is needed, please add a comment “`/unresolve`” to reopen the issue."

## Lock closed issues

### Trigger

- CRON (every 6 hours)

### Criteria

- Issue is closed
- Issue was last updated more than 90 days ago

### Actions

- Lock issue conversations
