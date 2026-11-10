# Export Teams Channel History

Export historical Teams conversations for analysis and evaluation-dataset
preparation, including channels the bot does not monitor. Each exported thread
contains the root post and all replies, preserving HTML and attachment metadata.
Images and files are referenced, not downloaded.

**JSON export is the default.** Cosmos DB storage is optional. This capability
does not generate evaluation cases.

## Export to JSON

Use the chatbot development Python environment with the agent dependencies
installed. The collection Logic App must be deployed, your account must be
authorized to invoke it, and its Teams connection must have access to the channel.
Deployment uses [logicapp-cd.yml](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/azure-sdk-qa-bot-agent/pipelines/logicapp-cd.yml)
with `workflow=teams-collection` and the target `environment`.

Obtain the callback URL **without SAS query parameters** and the application's
ID URI from the target environment's configuration. From
`tools\sdk-ai-bots\azure-sdk-qa-bot-agent`, replace the placeholders and run:

```powershell
az login --tenant "{directory_tenant_id}"

python scripts\collect_teams_history.py `
  --team-id "{team_guid}" `
  --channel-id "{channel_id}" `
  --logic-app-url "{sas_free_callback_url}" `
  --logic-app-scope "{application_id_uri}/.default" `
  --start-time "2026-01-01T00:00:00Z" `
  --output "C:\history\threads.json"
```

`--start-time` selects posts created at or after the cutoff, including all their
replies. Omit it to collect all available history. Optional `--max-pages` limits
root-post pages; reaching the limit while more pages remain fails the export.

The output directory must exist and the file must be new. A successful run prints
the output path and thread/message counts. This command does not access Cosmos DB.

## Also Save to Cosmos DB

Set `AZURE_APPCONFIG_ENDPOINT` to the target environment's App Configuration
endpoint, then add these arguments to the export command:

```powershell
  --save-to-cosmos --tenant-id "{bot_tenant_id}"
```

Use the channel's existing bot tenant ID, or confirm one for a new channel.
Your account needs App Configuration read access and Cosmos data-read/create
access. **Only missing messages are created; existing records are never
overwritten.** The JSON file is still exported.
