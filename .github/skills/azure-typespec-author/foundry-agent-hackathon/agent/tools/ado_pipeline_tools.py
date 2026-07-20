"""Azure DevOps pipeline tools for the Self-Evolving Agent.

Lets the workflow trigger and read the **Vally code-quality benchmark** as an
ADO pipeline run (default pipeline id ``8178`` in ``azure-sdk/internal`` —
https://dev.azure.com/azure-sdk/internal/_build?definitionId=8178) instead of
running ``vally`` locally. This is step 3 of the coded workflow.

Authentication (mirrors ``tools/sdk-ai-bots/azure-sdk-qa-bot-agent`` —
``utils/ado_token.py`` + ``tools/pipeline_tools.py``), resolved in order:

1. ``ADO_PAT`` (or ``AZURE_DEVOPS_EXT_PAT``) — an Azure DevOps PAT, sent as
   HTTP Basic auth. Simplest for local / personal use.
2. ``ADO_TOKEN`` — a pre-minted AAD **bearer** token (JWT) for the ADO resource,
   e.g. refreshed out-of-band into an env var (the qa-bot reads this from Key
   Vault; here it comes from the environment / ``.env``).
3. ``ADO_TOKEN_KEYVAULT_URL`` — **online/hosted path.** Read the bearer token from
   a Key Vault secret (name ``ado-token``, override with ``ADO_TOKEN_SECRET_NAME``)
   using the container's managed identity. The identity only needs Key Vault
   *get* on the secret — an out-of-band job (the qa-bot's ``AdoTokenRefresh``
   timer function) keeps the secret fresh, so the agent need not be an ADO org
   member itself.
4. ``DefaultAzureCredential`` — mints an AAD token for the ADO resource scope
   ``499b84ac-1321-427f-aa17-267ca6975798/.default`` using the ambient identity
   (``az login`` locally, or the managed identity in the container). Requires
   the identity to be an ADO org member.

The token is read from the environment or a ``.env`` file next to the agent.
"""

from __future__ import annotations

import base64
import json
import logging
import os
import time
from pathlib import Path
from typing import Annotated, Any, Optional

import httpx

logger = logging.getLogger(__name__)

_ADO_ORG_URL = "https://dev.azure.com/azure-sdk"
_DEFAULT_PROJECT = "internal"
_DEFAULT_PIPELINE_ID = 8178
_API_VERSION = "7.1"
_TIMEOUT_SECONDS = 30
# AAD resource id for Azure DevOps (constant across all orgs).
_ADO_RESOURCE_SCOPE = "499b84ac-1321-427f-aa17-267ca6975798/.default"
_ENV_CANDIDATES = (
    Path(__file__).resolve().parent.parent / ".env",
    Path(__file__).resolve().parent.parent.parent / ".env",
)

# Cached AAD credential + token (only used for the DefaultAzureCredential path).
_AAD_CACHE: dict[str, Any] = {"token": None, "expires_at": 0.0}
# Cached Key Vault-sourced bearer token (used for the online/hosted path).
_KV_CACHE: dict[str, Any] = {"token": None, "expires_at": 0.0}


def _load_env_files() -> None:
    try:
        from dotenv import load_dotenv

        for candidate in _ENV_CANDIDATES:
            if candidate.is_file():
                load_dotenv(candidate, override=False)
        return
    except Exception:
        pass
    for candidate in _ENV_CANDIDATES:
        if not candidate.is_file():
            continue
        for line in candidate.read_text(encoding="utf-8").splitlines():
            line = line.strip()
            if not line or line.startswith("#") or "=" not in line:
                continue
            key, _, value = line.partition("=")
            os.environ.setdefault(key.strip(), value.strip().strip("'\""))


def _aad_bearer_token() -> Optional[str]:
    """Mint (and cache) an AAD bearer token for ADO via DefaultAzureCredential."""
    now = time.time()
    if _AAD_CACHE["token"] and _AAD_CACHE["expires_at"] - 300 > now:
        return _AAD_CACHE["token"]
    try:
        from azure.identity import DefaultAzureCredential

        cred = DefaultAzureCredential()
        access = cred.get_token(_ADO_RESOURCE_SCOPE)
        _AAD_CACHE["token"] = access.token
        _AAD_CACHE["expires_at"] = float(access.expires_on)
        return access.token
    except Exception as exc:
        logger.warning("Could not mint ADO AAD token via DefaultAzureCredential: %s", exc)
        return None


def _jwt_exp_seconds(token: str) -> Optional[int]:
    """Return the ``exp`` claim (Unix seconds) from a JWT bearer token, or ``None``."""
    parts = token.split(".")
    if len(parts) < 2:
        return None
    payload_b64 = parts[1]
    padding = "=" * (-len(payload_b64) % 4)
    try:
        payload = json.loads(base64.urlsafe_b64decode(payload_b64 + padding))
    except Exception:
        return None
    exp = payload.get("exp")
    return int(exp) if isinstance(exp, (int, float)) else None


def _kv_ado_token() -> Optional[str]:
    """Read an ADO bearer token from a Key Vault secret (JIT-cached).

    Enables the **online (Foundry-hosted)** path: the container's managed identity
    cannot mint an ADO token itself (it is not an ADO org member), so an
    out-of-band job publishes a fresh token to Key Vault and the agent consumes it
    here. Mirrors ``azure-sdk-qa-bot-agent/utils/ado_token.py``.

    Enabled by setting ``ADO_TOKEN_KEYVAULT_URL`` (e.g.
    ``https://kv-haoling-hackathon.vault.azure.net/``); the secret name defaults to
    ``ado-token`` and can be overridden with ``ADO_TOKEN_SECRET_NAME``. The token is
    reused until 5 minutes before its JWT ``exp`` claim.
    """
    kv_url = (os.environ.get("ADO_TOKEN_KEYVAULT_URL") or "").strip()
    if not kv_url:
        return None
    now = time.time()
    if _KV_CACHE["token"] and _KV_CACHE["expires_at"] - 300 > now:
        return _KV_CACHE["token"]
    secret_name = (os.environ.get("ADO_TOKEN_SECRET_NAME") or "ado-token").strip()
    try:
        from azure.identity import DefaultAzureCredential
        from azure.keyvault.secrets import SecretClient

        client = SecretClient(vault_url=kv_url, credential=DefaultAzureCredential())
        secret = client.get_secret(secret_name)
        token = (secret.value or "").strip()
    except Exception as exc:
        logger.warning("Could not read ADO token from Key Vault %s: %s", kv_url, exc)
        return None
    if not token:
        logger.warning("Key Vault secret '%s' in %s is empty", secret_name, kv_url)
        return None
    exp_unix = _jwt_exp_seconds(token)
    ttl = max(exp_unix - int(now) - 300, 0) if exp_unix is not None else 300
    _KV_CACHE["token"] = token
    _KV_CACHE["expires_at"] = now + ttl
    return token


def _auth_header() -> Optional[dict[str, str]]:
    """Resolve an ADO Authorization header (PAT → AAD env → KeyVault → DefaultAzureCredential).

    A statically-injected ``ADO_TOKEN`` bearer is only used while it is still valid:
    if its JWT ``exp`` is in the past (or within 120s) it is **skipped** so the
    resolver falls through to the Key Vault / managed-identity paths, which mint or
    read a fresh token. This lets a long (~1h) benchmark poll survive past the
    lifetime of the token injected at deploy time.
    """
    _load_env_files()
    pat = (os.environ.get("ADO_PAT") or os.environ.get("AZURE_DEVOPS_EXT_PAT") or "").strip()
    if pat:
        basic = base64.b64encode(f":{pat}".encode("utf-8")).decode("ascii")
        return {"Authorization": f"Basic {basic}"}
    bearer = (os.environ.get("ADO_TOKEN") or "").strip()
    if bearer:
        exp = _jwt_exp_seconds(bearer)
        if exp is None or exp - 120 > time.time():
            return {"Authorization": f"Bearer {bearer}"}
        logger.warning("Injected ADO_TOKEN is expired; falling back to Key Vault / managed identity.")
    kv_token = _kv_ado_token()
    if kv_token:
        return {"Authorization": f"Bearer {kv_token}"}
    token = _aad_bearer_token()
    if token:
        return {"Authorization": f"Bearer {token}"}
    return None


def _err(message: str, **extra: Any) -> str:
    return json.dumps({"status": "error", "message": message, **extra}, ensure_ascii=False)


def _need_auth() -> Optional[dict[str, str]]:
    header = _auth_header()
    if header is None:
        return None
    return header


def _run_web_url(run: dict) -> str:
    try:
        return run.get("_links", {}).get("web", {}).get("href", "")
    except Exception:
        return ""


# ---------------------------------------------------------------------------
# Code-facing helpers (return dicts) — used by workflow.py.
# ---------------------------------------------------------------------------


def trigger_pipeline_run(
    branch: str,
    pipeline_id: int = _DEFAULT_PIPELINE_ID,
    project: str = _DEFAULT_PROJECT,
    variables: Optional[dict[str, Any]] = None,
    run_ref: str = "",
) -> dict[str, Any]:
    """Queue a run of pipeline 8178 **on the updated skill ``branch``**.

    The benchmark (``azure-typespec-author-benchmark``) checks out its ``self`` repo
    and also declares a ``SkillBranch`` parameter used to overlay ``SKILL.md`` +
    ``references/`` from ``origin/<SkillBranch>``. We run the pipeline **on ``branch``
    itself** (``self`` refName = ``branch``, so the eval harness and suites come from
    the branch too) and **also pass the full ``branch`` as the ``SkillBranch`` template
    parameter**.

    Passing ``SkillBranch`` explicitly is required: the pipeline's own default is
    ``$(Build.SourceBranchName)``, which drops everything before the last ``/`` — so a
    ``self-evolve/<x>`` head branch would be fetched as just ``<x>`` and fail with
    ``couldn't find remote ref``.

    ``branch`` must exist on the **upstream ``Azure/azure-sdk-tools``** repo. Uses the
    Pipelines Runs API: ``POST {org}/{project}/_apis/pipelines/{pipelineId}/runs``.
    """
    header = _auth_header()
    if header is None:
        return {"status": "error", "message": "No ADO credentials (ADO_PAT / ADO_TOKEN / az login)."}
    skill_branch = branch[len("refs/heads/"):] if branch.startswith("refs/heads/") else branch
    # Run the pipeline ON the updated branch (self-checkout = branch), unless the
    # caller overrides run_ref (e.g. to run the pipeline definition from main).
    effective_ref = run_ref or branch
    ref = effective_ref if effective_ref.startswith("refs/") else f"refs/heads/{effective_ref}"
    body: dict[str, Any] = {
        "resources": {"repositories": {"self": {"refName": ref}}},
        "templateParameters": {"SkillBranch": skill_branch},
    }
    if variables:
        body["variables"] = {k: {"value": str(v)} for k, v in variables.items()}
    url = f"{_ADO_ORG_URL}/{project}/_apis/pipelines/{pipeline_id}/runs?api-version={_API_VERSION}"
    try:
        with httpx.Client(timeout=_TIMEOUT_SECONDS) as client:
            resp = client.post(
                url,
                headers={**header, "Content-Type": "application/json", "Accept": "application/json"},
                json=body,
            )
        if resp.status_code == 203:
            return {"status": "error", "message": "ADO returned 203 (not authorized). Check the token/scope."}
        if resp.status_code not in (200, 201):
            return {"status": "error", "message": f"trigger failed {resp.status_code}: {resp.text[:400]}"}
        run = resp.json()
        return {
            "status": "ok",
            "run_id": run.get("id"),
            "pipeline_id": pipeline_id,
            "project": project,
            "skill_branch": skill_branch,
            "state": run.get("state"),
            "web_url": _run_web_url(run),
        }
    except Exception as exc:
        return {"status": "error", "message": f"{type(exc).__name__}: {str(exc)[:300]}"}


def get_pipeline_run(
    run_id: int,
    pipeline_id: int = _DEFAULT_PIPELINE_ID,
    project: str = _DEFAULT_PROJECT,
) -> dict[str, Any]:
    """Read a pipeline run's state/result. Returns a result dict."""
    header = _auth_header()
    if header is None:
        return {"status": "error", "message": "No ADO credentials."}
    url = f"{_ADO_ORG_URL}/{project}/_apis/pipelines/{pipeline_id}/runs/{run_id}?api-version={_API_VERSION}"
    try:
        with httpx.Client(timeout=_TIMEOUT_SECONDS) as client:
            resp = client.get(url, headers={**header, "Accept": "application/json"})
        if resp.status_code == 203:
            return {"status": "error", "message": "ADO returned 203 (not authorized)."}
        resp.raise_for_status()
        run = resp.json()
        return {
            "status": "ok",
            "run_id": run_id,
            "state": run.get("state"),  # inProgress | completed | ...
            "result": run.get("result"),  # succeeded | failed | canceled | None
            "web_url": _run_web_url(run),
        }
    except Exception as exc:
        return {"status": "error", "message": f"{type(exc).__name__}: {str(exc)[:300]}"}


def wait_for_pipeline_run(
    run_id: int,
    pipeline_id: int = _DEFAULT_PIPELINE_ID,
    project: str = _DEFAULT_PROJECT,
    timeout_secs: int = 3600,
    poll_secs: int = 30,
) -> dict[str, Any]:
    """Poll a pipeline run until it completes or *timeout_secs* elapses."""
    deadline = time.time() + timeout_secs
    last: dict[str, Any] = {"status": "error", "message": "no poll performed"}
    while time.time() < deadline:
        last = get_pipeline_run(run_id, pipeline_id, project)
        if last.get("status") != "ok":
            return last
        if last.get("state") == "completed":
            return last
        logger.info("ADO run %s state=%s — waiting...", run_id, last.get("state"))
        time.sleep(poll_secs)
    last["timed_out"] = True
    return last


def download_pipeline_results(
    run_id: int,
    project: str = _DEFAULT_PROJECT,
    artifact_name: Optional[str] = None,
    max_chars: int = 100_000,
) -> dict[str, Any]:
    """Download a build artifact zip and return the concatenated text of the
    ``*.jsonl`` / ``*.md`` files inside it (the Vally results / summary).

    The Pipelines run id equals the Build id in azure-sdk, so this uses the
    Build Artifacts API: ``{org}/{project}/_apis/build/builds/{runId}/artifacts``.
    If *artifact_name* is omitted, the first artifact is used.
    """
    import io
    import zipfile

    header = _auth_header()
    if header is None:
        return {"status": "error", "message": "No ADO credentials."}
    list_url = (
        f"{_ADO_ORG_URL}/{project}/_apis/build/builds/{run_id}/artifacts?api-version={_API_VERSION}"
    )
    try:
        with httpx.Client(timeout=_TIMEOUT_SECONDS, follow_redirects=True) as client:
            listing = client.get(list_url, headers={**header, "Accept": "application/json"})
            if listing.status_code == 203:
                return {"status": "error", "message": "ADO returned 203 (not authorized)."}
            listing.raise_for_status()
            artifacts = listing.json().get("value", [])
            if not artifacts:
                return {"status": "ok", "found": False, "message": f"No artifacts on run {run_id}."}
            match = None
            if artifact_name:
                match = next((a for a in artifacts if a.get("name") == artifact_name), None)
            # Prefer the eval-results artifact (skip SDL/drop artifacts) when no
            # explicit name was given — the code-quality results are what the gate needs.
            if match is None:
                match = next(
                    (a for a in artifacts if str(a.get("name", "")).startswith("eval-results-code-quality")),
                    None,
                )
            if match is None:
                match = next(
                    (a for a in artifacts if str(a.get("name", "")).startswith("eval-results")),
                    None,
                )
            match = match or artifacts[0]
            download_url = match.get("resource", {}).get("downloadUrl")
            if not download_url:
                return {"status": "error", "message": "Artifact has no downloadUrl."}
            dl = client.get(download_url, headers=header)
            dl.raise_for_status()
            parts: list[str] = []
            total = 0
            with zipfile.ZipFile(io.BytesIO(dl.content)) as zf:
                # Read the human summary (eval-results.md) and any top-level results
                # JSONL *before* the large per-turn executor event logs, so the
                # verdict table is never dropped by the max_chars truncation.
                def _priority(n: str) -> int:
                    low = n.lower()
                    if low.endswith("eval-results.md"):
                        return 0
                    if low.endswith(".md"):
                        return 1
                    if "events.jsonl" in low or "executor-session-logs" in low:
                        return 4
                    if low.endswith((".jsonl", ".json")):
                        return 2
                    return 3
                names = sorted(zf.namelist(), key=lambda n: (_priority(n), n))
                for name in names:
                    if name.endswith("/") or not name.endswith((".jsonl", ".md", ".txt", ".json")):
                        continue
                    text = zf.read(name).decode("utf-8", errors="replace")
                    chunk = f"\n===== {name} =====\n{text}"
                    if total + len(chunk) > max_chars:
                        chunk = chunk[: max_chars - total]
                        parts.append(chunk)
                        break
                    parts.append(chunk)
                    total += len(chunk)
            return {
                "status": "ok",
                "found": True,
                "artifact": match.get("name"),
                "content": "".join(parts) or "(no text files in artifact)",
            }
    except Exception as exc:
        return {"status": "error", "message": f"{type(exc).__name__}: {str(exc)[:300]}"}


# ---------------------------------------------------------------------------
# Agent-facing tool (returns a JSON string) — lets the agentic path trigger too.
# ---------------------------------------------------------------------------


def trigger_ado_pipeline(
    branch: Annotated[str, "Branch (or full refName) to run the pipeline on, e.g. the draft PR head branch."],
    pipeline_id: Annotated[int, "ADO pipeline/definition id. Default 8178 (the code-quality benchmark)."] = _DEFAULT_PIPELINE_ID,
    project: Annotated[str, "ADO project. Default 'internal'."] = _DEFAULT_PROJECT,
) -> str:
    """Trigger the ADO code-quality benchmark pipeline (Step 2, remote).

    Queues a run of pipeline ``8178`` (``azure-typespec-author-benchmark``) in
    ``azure-sdk/internal`` on *branch* — this runs the Vally code-quality (forced)
    evals that the agent cannot run locally in its container. The pipeline checks
    out ``Azure/azure-sdk-tools`` and overlays ``SKILL.md`` + ``references/`` from
    *branch* (which therefore **must be pushed to the upstream ``Azure/azure-sdk-tools``
    repo**, not a fork — the overlay does ``git fetch origin <branch>``).

    Returns the run ``run_id``, ``state`` and, importantly, the ``web_url`` — the
    **benchmark run link** you must include in your final report. Poll it to
    completion with ``wait_for_ado_pipeline`` and read the results artifact with
    ``download_ado_pipeline_results`` before applying the pass-rate gate.
    """
    result = trigger_pipeline_run(branch=branch, pipeline_id=pipeline_id, project=project)
    return json.dumps(result, ensure_ascii=False)


def get_ado_pipeline_run(
    run_id: Annotated[int, "Pipeline run id returned by trigger_ado_pipeline."],
    pipeline_id: Annotated[int, "ADO pipeline id. Default 8178."] = _DEFAULT_PIPELINE_ID,
    project: Annotated[str, "ADO project. Default 'internal'."] = _DEFAULT_PROJECT,
) -> str:
    """Read one ADO benchmark run's ``state``/``result`` and ``web_url`` (Step 2).

    ``state`` is ``inProgress``/``completed``; ``result`` is
    ``succeeded``/``failed``/``canceled`` once complete. Returns a JSON string.
    """
    return json.dumps(get_pipeline_run(run_id, pipeline_id, project), ensure_ascii=False)


def wait_for_ado_pipeline(
    run_id: Annotated[int, "Pipeline run id returned by trigger_ado_pipeline."],
    pipeline_id: Annotated[int, "ADO pipeline id. Default 8178."] = _DEFAULT_PIPELINE_ID,
    project: Annotated[str, "ADO project. Default 'internal'."] = _DEFAULT_PROJECT,
    timeout_secs: Annotated[int, "Max seconds to wait for the run to complete. Default 1800."] = 1800,
    poll_secs: Annotated[int, "Seconds between status polls. Default 60."] = 60,
) -> str:
    """Poll the ADO benchmark run until it completes or *timeout_secs* elapses (Step 2).

    The code-quality benchmark is long-running (tens of minutes). If it does not
    finish within ``timeout_secs`` the result carries ``timed_out: true`` and the
    latest ``state`` — report the ``web_url`` so a human can follow the run, and do
    **not** open a PR (the gate cannot be evaluated without results). Returns a JSON
    string with ``state``, ``result`` and ``web_url``.
    """
    return json.dumps(
        wait_for_pipeline_run(run_id, pipeline_id, project, timeout_secs, poll_secs),
        ensure_ascii=False,
    )


def download_ado_pipeline_results(
    run_id: Annotated[int, "Pipeline run id (equals the ADO Build id) to pull results from."],
    project: Annotated[str, "ADO project. Default 'internal'."] = _DEFAULT_PROJECT,
    artifact_name: Annotated[str, "Optional artifact name; defaults to the first (e.g. eval-results-code-quality-<id>)."] = "",
) -> str:
    """Download the benchmark results artifact and return its text (Step 2/3).

    Concatenates the ``*.jsonl``/``*.md`` files in the run's ``eval-results-*``
    artifact. Feed the returned ``content`` to ``summarize_benchmark_results`` for
    the report and to ``open_draft_pr_if_benchmark_passed`` for the pass-rate gate.
    Returns a JSON string with ``found``, ``artifact`` and ``content``.
    """
    return json.dumps(
        download_pipeline_results(run_id, project, artifact_name or None),
        ensure_ascii=False,
    )
