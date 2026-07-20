# TypeSpec Authoring Skill — Self-Evolving Agent

Code for the AI Foundry agent described in
[`../foundry-agent-design.md`](../foundry-agent-design.md). It continuously improves the
`azure-typespec-author` skill: grounds edits in user telemetry + public docs, runs the Vally
benchmark, generates a report, and analyzes documentation gaps — emitting a PR for human
review.

## Stack

Built with the **Microsoft Agent Framework** and hosted on Foundry, matching
[`azure-sdk-qa-bot-agent`](../../../../../tools/sdk-ai-bots/azure-sdk-qa-bot-agent):

- `agent-framework-core` / `agent-framework-foundry` — the `Agent` + `FoundryChatClient`.
- `agent-framework-foundry-hosting` — `ResponsesHostServer`, serving the Responses protocol
  on port 8088 inside the container.
- `azure-ai-projects` — used by `deploy.py` to create the hosted agent version.

## Files

| File | Purpose |
| ---- | ------- |
| `init.py` | Container entrypoint: builds the `Agent`, registers tools, runs `ResponsesHostServer`. |
| `instruction.md` | System instructions (the four-step procedure, **remote-first**). |
| `agent.yaml` | Agent name metadata. |
| `tools/ado_pipeline_tools.py` | **Step 3 ADO pipeline tools**: `trigger_ado_pipeline` (agent tool) + `trigger_pipeline_run` / `wait_for_pipeline_run` / `download_pipeline_results` (code). Triggers the Vally code-quality benchmark (pipeline **8178**, `azure-sdk/internal`). |
| `workflow.py` | **Coded, benchmark-gated orchestrator** (Python counterpart of `agentic-doc-refinement/doc-refinement.mjs`): analyze prompts + update docs → **push a branch (no PR)** → ADO pipeline 8178 benchmark → compute pass rate → analyze + report → **open a draft PR only if pass rate > threshold**. |
| `workflow_prompts/*.md` | Per-step prompts driven by `workflow.py`. |
| `tools/github_mcp_tools.py` | **Read tools via GitHub's official MCP server** (`create_github_mcp_tool`): repo browse, code/PR/issue search, and Actions run/log inspection. PAT-authenticated (read from `.env`), pinned **read-only**. |
| `tools/foundry_toolbox_tools.py` | **Foundry toolbox (WorkIQ) MCP tool** (`create_foundry_toolbox_tool`) + `fetch_sharepoint_file_bytes` helper: reads **internal / SharePoint / OneDrive documents online** (e.g. the private telemetry Excel). `read_prompt_excel` uses the helper (metadata-by-url → read-binary-file) to fetch a SharePoint workbook without a local download. Auth via `DefaultAzureCredential` (`https://ai.azure.com/.default`) + `Foundry-Features: Toolboxes=V1Preview`. Enabled when `FOUNDRY_TOOLBOX_MCP_URL` or `AI_FOUNDRY_PROJECT_ENDPOINT` (+ `FOUNDRY_TOOLBOX_NAME`) is set. |
| `tools/github_tools.py` | **Remote write + fallback** tools: `push_skill_changes` (commit to a branch, **no PR**), `open_draft_pr` / `open_skill_pull_request` (force **draft**), `comment_on_pr`, `dispatch_workflow`, plus REST read fallbacks (`read_repo_file`, `list_repo_dir`, `get_latest_workflow_run`, `download_workflow_artifact`) used when the MCP tool is unavailable. |
| `tools/skill_evolution_tools.py` | `fetch_documentation`, `read_prompt_excel` (parse a user-prompt `.xlsx` from a local path, URL, or **SharePoint/OneDrive link** — SharePoint is read online via the Foundry toolbox, with a Microsoft Graph `GRAPH_TOKEN` fallback), `summarize_benchmark_results` / `compute_pass_rate` (mode-agnostic), `run_benchmark` (**local-only**). |
| `Dockerfile` | Azure Linux Python 3.12 image. |
| `deploy.py` | Builds the image with `az acr build` and creates a hosted agent version. |
| `requirements.txt` | Pinned SDK versions (aligned with the qa-bot agent). |

## Online (Foundry-hosted) vs. local

The agent must complete the **same** four-step workflow whether it runs remotely in
Foundry or locally on a dev box. The hosted container has **no repo checkout, no skill files
on disk, and no `vally` CLI**, so the workflow is **remote-first** and driven entirely through
the GitHub REST API:

| Step | Remote (Foundry-hosted) | Local (dev box with a checkout) |
| ---- | ----------------------- | ------------------------------- |
| 1. Update skill | GitHub MCP `get_file_contents` / `search_code` → `open_skill_pull_request` (draft) | same tools, or edit files directly || 2. Run benchmark | Opening the PR triggers the **`skill-eval` CI**; re-run via `dispatch_workflow` | `run_benchmark` (shells out to `vally`) |
| 3. Analyze results | GitHub MCP `actions_get` / `actions_get_job_logs` (or `download_workflow_artifact`) → `summarize_benchmark_results(content=…)` | `summarize_benchmark_results(results_path=…)` |
| 4. Doc gaps | `fetch_documentation` / `web_search` (reasoning) | same |

The agent never merges — it opens a PR (`open_skill_pull_request`) bundling the skill diff,
the benchmark report, and the gap analysis for a human reviewer.

## Configuration (environment variables)

| Variable | Required | Description |
| -------- | -------- | ----------- |
| `AI_FOUNDRY_PROJECT_ENDPOINT` | yes | `https://{account}.services.ai.azure.com/api/projects/{project}` |
| `AI_FOUNDRY_AGENT_MODEL` | yes | Model deployment name, e.g. `gpt-5.4-1` |
| `AI_FOUNDRY_AGENT_REASONING_EFFORT` | no | `low` \| `medium` \| `high` (default `medium`) |
| `APP_VERSION` | no | Version appended to the agent id for tracing |
| `GITHUB_TOKEN` (or `GH_TOKEN`) | one of these | **PAT mode.** Read from the environment or a local **`.env`** file. Used by the read-only **GitHub MCP tool** and the custom draft-PR / workflow-dispatch write tools. Needs `contents:write`, `pull_requests:write`, `actions:read`/`write`. See `.env.example`. |
| `GITHUB_APP_ID` + `GITHUB_APP_INSTALLATION_ID` + `GITHUB_APP_PRIVATE_KEY` (or `..._BASE64`) | one of these | **GitHub App mode (recommended).** The agent signs an RS256 JWT and mints a short-lived (1h) installation token itself — no personal PAT. Grant the App: Contents RW, Pull requests RW, Actions RW on the repo. |
| `GITHUB_REPO` | no | `owner/name` the agent operates on (default `Azure/azure-sdk-tools`). |
| `ADO_PAT` (or `AZURE_DEVOPS_EXT_PAT`) | one of these (step 3) | **Azure DevOps PAT** (Basic auth) to trigger/read pipeline **8178**. Simplest for local use; read from `.env`. |
| `ADO_TOKEN` | one of these (step 3) | Pre-minted **AAD bearer** token for the ADO resource (`499b84ac-...`). |
| `ADO_TOKEN_KEYVAULT_URL` (+ optional `ADO_TOKEN_SECRET_NAME`, default `ado-token`) | one of these (step 3) | **Online/hosted path (recommended).** The container's managed identity reads the ADO bearer token from a Key Vault secret. The MI only needs Key Vault **get** on the secret; an out-of-band job (the qa-bot `AdoTokenRefresh` timer function) keeps it fresh — so the agent itself need not be an ADO org member. |
| _(none)_ → `DefaultAzureCredential` | fallback (step 3) | If neither ADO var is set, an AAD token is minted from the ambient identity (`az login` / managed identity). Requires ADO org membership. |
| `TYPESPEC_EVALUATE_DIR` | no | Local mode only: path to `azure-typespec-author/evaluate` for `run_benchmark`. |
| `FOUNDRY_TOOLBOX_MCP_URL` | one of these (SharePoint) | Full Foundry toolbox (WorkIQ) MCP URL, e.g. `.../toolboxes/read-internal-docs/mcp?api-version=v1`. Enables the agent to read **private SharePoint/OneDrive Excel** online. |
| `FOUNDRY_TOOLBOX_NAME` (+ optional `FOUNDRY_TOOLBOX_VERSION`) | one of these (SharePoint) | Toolbox name (default `read-internal-docs`); the URL is built from `AI_FOUNDRY_PROJECT_ENDPOINT`. `FOUNDRY_TOOLBOX_VERSION` adds a `/versions/{n}` segment. The toolbox uses `DefaultAzureCredential` (`https://ai.azure.com/.default`) — grant that identity access to the toolbox/SharePoint content. |

## Coded workflow (`workflow.py`)

`workflow.py` runs the steps deterministically (the reasoning steps are agent turns; the
benchmark is an ADO pipeline trigger and the PR gate is a pass-rate check), so the **same
code runs online in the container or locally**. **The draft PR is opened last, only if the
benchmark clears the threshold:**

| Step | What runs | How |
| ---- | --------- | --- |
| 1. Analyze prompts + update reference docs + skill | agent turn | reads an optional user-prompt Excel (`--prompts-excel`), then **pushes a branch (no PR)** via `push_skill_changes` |
| 2. Run code-quality (forced) evals | code | triggers **ADO pipeline 8178** (`azure-sdk/internal`) on the pushed branch, waits, downloads the results artifact |
| 3. Compute the benchmark pass rate | code | `compute_pass_rate` on the artifact |
| 4. Analyze results / attribute gaps | agent turn | fed the pipeline artifact content |
| 5. Generate the gap report | agent turn | becomes the PR body |
| **Gate** | code | opens a **draft PR** via `open_draft_pr` **only if** `pass_rate > --min-pass-rate` (default **75%**); otherwise skips the PR and logs the report |

```bash
# full loop (analyze an Excel of user prompts, gate the PR at 75%)
python workflow.py --project-endpoint "$AI_FOUNDRY_PROJECT_ENDPOINT" --model "$AI_FOUNDRY_AGENT_MODEL" \
    --prompts-excel ./user-prompts.xlsx --min-pass-rate 75
# rerun benchmark + gate against an already-pushed branch (skip step 1)
python workflow.py --skip-docs --branch self-evolve/paging
# feed a local results file into steps 3-5 (skip the ADO run)
python workflow.py --skip-eval --results-file ./results.jsonl --branch self-evolve/paging
```

> The `--prompts-excel` source may be a **local path**, a **direct-download URL**, or a
> **private SharePoint/OneDrive link**. SharePoint/OneDrive links are read online via the
> **Foundry toolbox (WorkIQ)** — configure `FOUNDRY_TOOLBOX_MCP_URL` (see above) and grant
> the running identity access; no local download or personal token is needed. (A Microsoft
> Graph token via `GRAPH_TOKEN` with `Files.Read.All` is a fallback.) Requires `openpyxl`
> (in `requirements.txt`).

## Run locally

```bash
pip install --pre -r requirements.txt
export AI_FOUNDRY_PROJECT_ENDPOINT="https://renhel-demo1-resource.services.ai.azure.com/api/projects/renhel-demo1"
export AI_FOUNDRY_AGENT_MODEL="gpt-5.4-1"
python init.py     # serves the Responses protocol on :8088
```

## Deploy (hosted container agent)

```bash
python deploy.py \
  --project-endpoint "https://foundry-haoling-eus2.services.ai.azure.com/api/projects/proj-default" \
  --acr haolingfoundryagentacr \
  --model gpt-5.6-sol \
  --github-token "$GITHUB_TOKEN"
```

`deploy.py` builds the image server-side with `az acr build` (no local Docker) and calls
`project.agents.create_version(...)` with a `HostedAgentDefinition`. The Foundry project's
managed identity must have **AcrPull** on the registry.

**Give the agent GitHub access** so it can read the repo, open PRs, and read the benchmark CI
remotely. Prefer a **GitHub App** (no personal PAT, scoped install permissions, short-lived
tokens):

```bash
python deploy.py \
  --project-endpoint "https://foundry-haoling-eus2.services.ai.azure.com/api/projects/proj-default" \
  --acr haolingfoundryagentacr --model gpt-5.6-sol \
  --github-app-id 123456 \
  --github-app-installation-id 7890123 \
  --github-app-private-key-file ./typespec-self-evolve.private-key.pem
```

`deploy.py` base64-encodes the `.pem` and injects `GITHUB_APP_ID` /
`GITHUB_APP_INSTALLATION_ID` / `GITHUB_APP_PRIVATE_KEY_BASE64`. At runtime the agent signs an
RS256 JWT with the App key and exchanges it for a 1-hour installation token (cached). Create
the App with **Contents: Read & write**, **Pull requests: Read & write**, **Actions: Read &
write**, install it on the repo, and copy the installation ID from the install URL.

A PAT still works for a quick manual setup (`--github-token "$GITHUB_TOKEN"`); if both are
provided the PAT wins.
