function Get-GithubProjectId([string] $project)
{
  # project should be ine one of the following formats
  # https://github.com/orgs/<org>/projects/<number>
  # https://github.com/users/<user>/projects/<number>
  # or just a number in which case default to Azure as the org
  $projectId = ""
  if ($project -match "((orgs/(?<org>.*))|(users/(?<user>.*))/projects/)?(?<number>\d+)$")
  {
    $projectNumber = $matches["number"]
    if ($matches["user"]) {
      $name = $matches["user"]
      $projectQuery = 'query($name: String!, $number: Int!) { user(login: $name) { projectNext(number: $number) { id } } }'
      $selectQuery = ".data.user.projectNext.id"
    }
    else {
      $name = $matches["org"]
      $name ??= "Azure"

      $projectQuery = 'query($name: String!, $number: Int!) { organization(login: $name) { projectNext(number: $number) { id } } }'
      $selectQuery = ".data.organization.projectNext.id"
    }

    $projectId = gh api graphql -f query=$projectQuery -F name=$name -F number=$projectNumber --jq $selectQuery
  }
  return $projectId
}

function Add-GithubIssueToProject([string]$projectId, [string]$issueId)
{
  $projectItemId = gh api graphql -F projectId=$projectId -F issueId=$issueId -f query='
    mutation($projectId: ID!, $issueId: ID!) {
      addProjectNextItem(input: {projectId: $projectId, contentId: $issueId}) {
        projectNextItem {
          id
        }
      }
    }' --jq ".data.addProjectNextItem.projectNextItem.id"

  return $projectItemId
}

function Remove-GithubIssueFromProject([string]$projectId, [string]$projectItemId)
{
  $projectDeletedItemId = gh api graphql -F projectId=$projectId -F itemId=$projectItemId -f query='
    mutation($projectId: ID!, $itemId: ID!)  {
      deleteProjectNextItem(input: {projectId: $projectId, itemId: $itemId} ) {
        deletedItemId
      }
  }' --jq ".data.deleteProjectNextItem.deletedItemId"

  return $projectDeletedItemId
}
