# AI Foundry — Hackathon Feedback

**Author:** @haolingdong-msft
**Date:** 2026-07-17
**Context:** Built a hosted **Self-Evolving Skill Agent** on AI Foundry during a hackathon.
The agent continuously improves the [`azure-typespec-author`](../SKILL.md) skill: it reads
real user telemetry (an Excel workbook), grounds edits in public docs, pushes a branch,
runs a benchmark pipeline, and opens a draft PR with a gap analysis. See
[`foundry-agent-design.md`](./foundry-agent-design.md) and [`agent/README.md`](./agent/README.md).

This document is **field feedback for the AI Foundry team**. It records the workflow I
actually went through, and the friction I hit — the large majority of which was
**authentication and authorization**, not modeling or agent logic.

---

## 1. What I was trying to build

A single hosted (containerized) Foundry agent that runs a 4-step autonomous loop:

1. **Read telemetry** — a user-prompt Excel exported via WorkIQ, stored on **SharePoint**.
2. **Update the skill** — read/write files in a **GitHub** repo (`Azure/azure-sdk-tools`),
   pushing a branch and opening a draft PR.
3. **Run the benchmark** — trigger and poll an **Azure DevOps (ADO)** pipeline, then
   download its results artifact.
4. **Analyze + report** — ground with public docs / web search and emit a gap analysis.

So the agent needs to reach **four external systems**: SharePoint, GitHub, ADO, and public
web. Three of those four are behind corporate auth. That is where almost all the pain was.

---

## 2. The process I went through (abridged)

1. Wrote the agent with the Microsoft Agent Framework + `FoundryChatClient`, hosted via
   `ResponsesHostServer`, deployed with `az acr build` + `create_version(HostedAgentDefinition)`.
2. **Fought ACR pull permissions** — the Foundry project's managed identity needed
   **AcrPull** on the registry before the container would even start.
3. **Fought every downstream resource's auth** one by one (details below).
4. Ended up with a **different auth mechanism per system**, none of them "on behalf of me":
   - GitHub → a **GitHub App** (or PAT) injected as a container env var.
   - ADO → a **bearer token stashed in Key Vault**, read by the container's managed identity,
     kept fresh by an out-of-band timer function.
   - SharePoint → could **not** get the hosted identity to read it at all; worked around it
     with a **WorkIQ Toolbox (MCP)** and, locally, a pasted Graph token.
   - Local files → **no access from the hosted agent**, full stop.

The agent logic was the easy part. Wiring identity to four systems took the majority of the
hackathon.

---

## 3. Top problems (ranked)

### 3.1 Granting roles to the Foundry agent identity is opaque and slow

- It was not obvious **which identity** the hosted agent actually runs as (the Foundry
  project's managed identity), nor **where** to grant it roles.
- The first blocker is mundane but total: **AcrPull** on the registry. Until that role
  propagated, the container simply would not start, with errors that didn't clearly say
  "this is an ACR RBAC problem."
- Role assignment + propagation delay made the inner loop slow: deploy → wait → discover a
  missing role → assign → wait again.

**Ask:** In the Foundry portal, surface the hosted agent's identity prominently and offer a
**one-click "grant this identity access to X"** flow (ACR, Key Vault, Storage, etc.), plus a
**preflight check** that lists every role the agent is missing for its configured tools
*before* I deploy.

### 3.2 A hosted agent reaching *internal* materials is very hard

This was the biggest theme. Each internal system needed a bespoke, hand-rolled solution:

- **SharePoint / OneDrive (telemetry Excel):** the hardest. A plain download of a sharing
  link returns **401**. `DefaultAzureCredential` from `az login` mints a Graph token from the
  **Azure CLI first-party app**, which does **not** carry `Files.Read`/`Sites.Read`, so
  Microsoft Graph `/shares/{id}/driveItem/content` returns **403** even for a file I own.
  There was no clean, documented "hosted agent reads a SharePoint file" path. I worked around
  it by wiring a **WorkIQ Toolbox** (Foundry toolbox exposed as MCP) that reads the document
  on my behalf, and — for local dev — by pasting a Graph token (`GRAPH_TOKEN`) with
  `Files.Read.All`.
- **GitHub:** no first-class "agent identity → GitHub" story. I had to create and install a
  **GitHub App** (sign a JWT, mint 1-hour installation tokens) or fall back to a **PAT**, then
  inject secrets as container env vars. Managing App private keys / PAT scopes / SAML-SSO
  authorization was all manual.
- **ADO pipeline:** ADO does not accept the managed identity easily unless it is an **ADO org
  member**. The practical workaround was to **store a bearer token in Key Vault** and have the
  container read it (managed identity needs only Key Vault *get*), with a **separate timer
  function** refreshing the token. That is a lot of moving parts just to trigger one pipeline.

**Ask:** First-class, documented **connectors for hosted agents** to the systems Microsoft
engineers actually use — SharePoint/OneDrive (Graph), GitHub, and Azure DevOps — where Foundry
handles the token exchange and scoping, instead of every team re-inventing App-JWT / Key-Vault-token /
MCP-toolbox glue.

### 3.3 No access to my local files

The hosted container has **no checkout, no local filesystem, and no local CLI** (e.g. `vally`).
Anything that was trivial locally ("just read the file / run the tool on disk") had to be
re-expressed as a **remote API call** (GitHub REST/MCP, ADO pipeline, toolbox). This forced a
"remote-first" redesign of the whole workflow and roughly doubled the tool surface (a local
path *and* a remote path for each step).

**Ask:** A supported way to give a hosted agent scoped access to a workspace/volume, or at
least clear guidance that hosted agents must be 100% remote so people design for it up front.

### 3.4 On-Behalf-Of (OBO) is unclear — and the fallback over-privileges the agent

This is the concern I care most about. I could not find a clear **best practice for the agent
to act on behalf of *me* (the invoking user)** and access exactly the resources *I* can see.
Every mechanism I got working instead grants the **agent's own identity** standing access:

- The GitHub App / PAT can read/write the repo regardless of who invoked the agent.
- The ADO token in Key Vault is a standing credential.
- To make SharePoint work broadly, the temptation is to grant the agent identity
  **`Files.Read.All` / `Sites.Read.All`** — i.e. read **all** files in the tenant — which is
  vastly more than "read the one file the user pointed me at."

That is the wrong security posture: **the agent ends up able to view far more than the user
who triggered it.** What I *want* is a token scoped to the caller's own permissions (true OBO /
delegated flow), so the agent can only touch what the user could already touch.

**Ask:** A documented, first-class **OBO / delegated-access pattern for Foundry agents** — the
user consents, Foundry exchanges the user token for a downstream (Graph/GitHub/ADO) token, and
the agent operates strictly within the caller's permissions. Make this the *default* and make
broad application permissions the exception.

---

## 4. Smaller papercuts

- **Per-system, per-environment auth divergence.** Local (`az login`) and hosted (managed
  identity) behave differently enough that "works locally" repeatedly failed once hosted. Auth
  needed a separate code path and separate testing for each environment.
- **Toolbox (Toolboxes=V1Preview) discoverability.** The WorkIQ toolbox ended up being the
  cleanest way to read SharePoint, but I only found it late; the preview header
  (`Foundry-Features: Toolboxes=V1Preview`) and the MCP URL shape are not obvious.
- **Binary reads through MCP.** Reading a 3.4 MB Excel through the toolbox returns base64 text;
  a naive result-truncation silently corrupted it, and an LLM can't parse an xlsx from base64
  anyway. I had to fetch the bytes in Python and parse them — a reminder that "let the model
  read the file" doesn't work for binary formats.
- **Error messages point at the symptom, not the cause.** 401/403/415 responses rarely said
  *which* identity was used or *which* scope/role was missing, which made every auth failure a
  guessing game.

---

## 5. What went well

- The core agent (Agent Framework + `FoundryChatClient` + `ResponsesHostServer`) was
  pleasant; modeling tools and the reasoning loop was straightforward.
- **Foundry Toolboxes (MCP)** turned out to be a genuinely good escape hatch for internal
  document access once discovered — it read the SharePoint Excel on my behalf without me
  minting any SharePoint/Graph token in the container. **This is close to the OBO experience I
  want; please invest in it and document it as the recommended path.**
- `az acr build` + hosted `create_version` deployment (no local Docker) was smooth once the
  AcrPull role was in place.

---

## 6. Summary of asks

1. **Surface the hosted agent's identity** and offer one-click role grants + a **preflight
   permissions check** before deploy.
2. **First-class connectors** for SharePoint/OneDrive, GitHub, and Azure DevOps from hosted
   agents — Foundry owns the token exchange, not every team.
3. **A supported story for local/workspace files**, or explicit "hosted = remote-only" guidance.
4. **A default OBO / delegated-access pattern** so the agent acts within the *caller's*
   permissions, instead of granting the agent identity broad standing access (e.g.
   `Files.Read.All` over the whole tenant).
5. **Better auth diagnostics** — errors that name the identity and the missing scope/role.
6. **Promote and document Foundry Toolboxes (MCP)** as the recommended way to reach internal
   content; it was the single best thing I found for on-behalf-of document access.
