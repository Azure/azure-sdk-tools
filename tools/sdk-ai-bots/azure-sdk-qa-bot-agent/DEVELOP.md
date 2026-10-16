# Developing and testing the chatbot

The **server** (`server.py`) handles API requests, tenant routing, and conversations.
The **agent** (`agents/chat_agent/`) runs in Foundry and answers questions using its
instructions and tools. You can change and test either component independently.

## Develop the agent

1. Make your changes on a working branch. Start with
   [agent instructions](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/azure-sdk-qa-bot-agent/agents/chat_agent/instruction.md),
   [tenant prompts](https://github.com/Azure/azure-sdk-tools/tree/main/tools/sdk-ai-bots/azure-sdk-qa-bot-agent/prompts/tenants), or [tools](https://github.com/Azure/azure-sdk-tools/tree/main/tools/sdk-ai-bots/azure-sdk-qa-bot-agent/tools), depending on the change.
   Commit and push the branch.
2. Open [azure-sdk-qa-bot-ai-foundry-agent-cd](https://dev.azure.com/azure-sdk/internal/_build?definitionId=8159)
   and select **Run pipeline**. Choose your working branch, set `environment` to
   `dev`, and select the agent you are developing.
3. Wait for deployment to finish. The log shows the created agent version and
   confirms when it is active. Dev deployment records your branch in the Foundry
   agent version's `metadata.branchname`.
   No branch registry is stored in App Configuration, and you do not need to update
   a version number in code. Older versions without branch metadata must be redeployed
   to use branch routing.
4. Test the deployed version using an evaluation pipeline or a dev channel message.

## Develop the server

Commit and push your server changes, then run
[Server CD](https://dev.azure.com/azure-sdk/internal/_build?definitionId=8128)
from your working branch with `environment=dev` and `deploymentTarget=server`.
This updates the shared dev backend for everyone; agent branch tags select an
agent version, not a separate backend. Server deployment automatically enables
branch routing in dev and disables it in preview and production.

After deployment, test through a dev channel or run an evaluation pipeline from
the same branch. Deploy the agents needed by your selected scenarios from that
branch before running evaluations.

## Run an evaluation pipeline

Open [Azure SDK Pipelines](https://dev.azure.com/azure-sdk/internal/_build) and
find the pipeline for your goal:

| Pipeline | Use it to |
|---|---|
| **tools - azure-sdk-ai-bot-evaluate-ci** | Check bot regressions against the basic scenarios. |
| **tools - azure-sdk-ai-bot-perf-ci** | Check bot improvements against the performance scenarios. |

Select **Run pipeline**, choose the **same branch you deployed**, and select the
`scenarios` you want to run. Both pipelines start a backend using dev configuration
and test your branch's deployed agent automatically. They do not deploy the agent.

After the run, inspect the published per-scenario results, including failed cases
and `server.log`. You can ask Copilot to analyze the failed cases, identify prompt
or tool changes, and improve the implementation. Push the changes, deploy the agent
again, and rerun the same scenarios to compare results.

See the [evaluation guide](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/azure-sdk-qa-bot-evaluation/README.md) for grading
details and result formats.

## Test in a dev channel

Deploy your agent branch first, then create a post in the dev channel with the
branch tag at the beginning of the title.

**Title**

```text
[branch:jiaqizhang-dev-api-stewardship-chatbot] test message
```

**Message**

```text
test
```

Replace the example branch with your working branch and the message with your
test question.

The tag selects your branch's newest active Foundry version for that agent and
message only, skipping failed or provisioning versions. If listing omits readiness,
the server checks version details; versions with unknown readiness are not selected.
Without a tag, the server uses the configured agent version, or latest if none is configured.
A missing branch deployment returns an error instead of using a different version.

Each branch-tagged request looks up the matching version directly in Foundry.

For deployment or runtime failures, see [TROUBLESHOOTING.md](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/azure-sdk-qa-bot-agent/TROUBLESHOOTING.md).
