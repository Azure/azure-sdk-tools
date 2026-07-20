# TypeSpec Authoring Skill — Feedback Agent

A hosted Foundry container agent whose single job is to **collect anonymized user telemetry**
from real `azure-typespec-author` sessions. The telemetry it records (user prompts + session
outcomes: whether the skill triggered, whether it asked clarifying questions, tool-call
errors, retries, success/failure, and free-text feedback) is exactly the "User telemetry"
input described in [`../foundry-agent-design.md`](../foundry-agent-design.md) that feeds the
sibling **Self-Evolving Agent**.

It is a companion to [`../agent/`](../agent) (the self-evolving agent): this one **gathers**
signals, the other **acts** on them.

## Where telemetry goes

Each record is written to the Foundry project's **Application Insights** as an OpenTelemetry
custom trace (`session.telemetry`) plus a `skill_session_telemetry` counter metric — the same
Application Insights already connected to the project for tracing. Query it from Log Analytics
(`traces` / `customMetrics`) or the Foundry Traces tab. No extra datastore is required.

## Stack

Built with the **Microsoft Agent Framework** and hosted on Foundry, matching the sibling
self-evolving agent:

- `agent-framework-core` / `agent-framework-foundry` — the `Agent` + `FoundryChatClient`.
- `agent-framework-foundry-hosting` — `ResponsesHostServer`, serving the Responses protocol
  on port 8088 inside the container.
- `azure-ai-projects` — used by `deploy.py` to create the hosted agent version.

## Files

| File | Purpose |
| ---- | ------- |
| `init.py` | Container entrypoint: builds the `Agent`, registers telemetry tools, runs `ResponsesHostServer`. |
| `instruction.md` | System instructions (collect + anonymize + record one telemetry item per session). |
| `agent.yaml` | Agent name metadata (`typespec-authoring-skill-feedback-agent`). |
| `tools/feedback_tools.py` | `record_session_telemetry`, `acknowledge_feedback`. |
| `Dockerfile` | Azure Linux Python 3.12 image (tracing env enabled). |
| `deploy.py` | Builds the image with `az acr build` and creates a hosted agent version. |
| `requirements.txt` | Pinned SDK versions (aligned with the self-evolving agent). |

## Configuration (environment variables)

| Variable | Required | Description |
| -------- | -------- | ----------- |
| `AI_FOUNDRY_PROJECT_ENDPOINT` | yes | `https://{account}.services.ai.azure.com/api/projects/{project}` |
| `AI_FOUNDRY_AGENT_MODEL` | yes | Model deployment name, e.g. `gpt-5.6-sol` |
| `AI_FOUNDRY_AGENT_REASONING_EFFORT` | no | `low` \| `medium` \| `high` (default `medium`) |
| `APP_VERSION` | no | Version appended to the agent id for tracing |

Tracing (Application Insights) is enabled automatically when the Foundry project has an
Application Insights connection — the platform injects the reserved
`APPLICATIONINSIGHTS_CONNECTION_STRING`.

## Run locally

```bash
pip install --pre -r requirements.txt
export AI_FOUNDRY_PROJECT_ENDPOINT="https://foundry-haoling-eus2.services.ai.azure.com/api/projects/proj-default"
export AI_FOUNDRY_AGENT_MODEL="gpt-5.6-sol"
python init.py     # serves the Responses protocol on :8088
```

## Deploy (hosted container agent)

```bash
python deploy.py \
  --project-endpoint "https://foundry-haoling-eus2.services.ai.azure.com/api/projects/proj-default" \
  --acr haolingfoundryagentacr \
  --model gpt-5.6-sol
```

`deploy.py` builds the image server-side with `az acr build` (no local Docker) and calls
`project.agents.create_version(...)` with a `HostedAgentDefinition`. The Foundry **project's**
managed identity must have **AcrPull** on the registry.

## Calling this agent from the `azure-typespec-author` skill (Step 7 hook)

The skill can report one telemetry record to this agent at the **end of every authoring
task**. Use the `send_feedback.py` client — it is **best-effort and non-blocking**: it always
exits 0, uses a short timeout, and degrades to a no-op if `az login`/deps/network are missing,
so telemetry can never fail or slow a real authoring task.

```bash
python send_feedback.py \
  --prompt "<anonymized user request gist>" \
  --outcome success|failure|partial \
  --skill-triggered true \
  --asked-clarifying-questions false \
  --tool-call-errors 0 \
  --retries 0 \
  --feedback "<optional free text>" \
  --session-id "<optional id>"
```

Target overrides: `--project-endpoint` / `--agent-name`, or env vars
`FEEDBACK_PROJECT_ENDPOINT`, `FEEDBACK_AGENT_NAME`, `FEEDBACK_TIMEOUT_S`.

**To wire it into the skill**, add a final step to `SKILL.md` (after Step 6):

> ### Step 7: Emit session telemetry (best-effort, on by default)
> At the end of every authoring task, run the feedback hook once to record an
> **anonymized** telemetry record. Runs by default; disable with
> `AZURE_TYPESPEC_AUTHOR_FEEDBACK=0`. This is best-effort — if it fails or is
> skipped, ignore it and never surface an error to the user:
> ```
> python .github/skills/azure-typespec-author/foundry-agent-hackathon/feedback-agent/send_feedback.py \
>   --prompt "<gist>" --outcome <success|failure|partial> \
>   --skill-triggered <true|false> --asked-clarifying-questions <true|false> \
>   --tool-call-errors <n> --retries <n>
> ```

### Caveats before enabling in the shared skill

- **Auth/RBAC**: every caller needs `az login` + access to *this* Foundry project. The
  endpoint above is a personal (hackathon) project, so it only works for people with RBAC on
  it. For a shared skill, point `FEEDBACK_PROJECT_ENDPOINT` at a team project.
- **Cost/latency**: each call is one model invocation on the hosted agent (a few seconds,
  longer on a cold container). The hook's timeout + always-exit-0 keep it from blocking.
- **Privacy**: pass only anonymized signals — never file contents, secrets, or PII.
