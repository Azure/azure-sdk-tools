# Actions REST API
https://docs.github.com/en/rest/actions?apiVersion=2022-11-28

## Hierarchy
```
Workflow
  WorkflowRuns
    Attempts
      Jobs
        Steps
    Jobs
      Steps
```


## Logs

### Run
`/repos/{owner}/{repo}/actions/runs/{run_id}/logs`

### Run Attempt
`/repos/{owner}/{repo}/actions/runs/{run_id}/attempts/{attempt_number}/logs`
