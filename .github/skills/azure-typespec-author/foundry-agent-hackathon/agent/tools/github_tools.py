"""GitHub-backed tools that let the Self-Evolving Agent complete its workflow
**remotely** (inside the Foundry hosted container), where the azure-sdk-tools
repository, the skill sources, and the ``vally`` CLI are all absent.

Why this exists
---------------
The hosted container ships only the agent code plus its Python dependencies. It
does **not** contain a checkout of ``Azure/azure-sdk-tools`` and cannot run the
Node-based ``vally`` benchmark locally. The filesystem tools in
``skill_evolution_tools.py`` (``run_benchmark``) therefore only work on a machine
that has the repo mounted (local mode). These GitHub tools give the agent a fully
remote path for every step:

    Step 1  Update the skill        -> read_repo_file / list_repo_dir  (read)
                                    -> open_skill_pull_request         (propose)
    Step 2  Run the benchmark       -> dispatch_workflow + the PR auto-triggers
                                       the `skill-eval` CI on `.github/skills/**`
    Step 3  Analyze the results     -> get_latest_workflow_run
                                    -> download_workflow_artifact
    Step 4  Analyze doc gaps        -> fetch_documentation / web_search (reasoning)

Authentication
--------------
All calls use the GitHub REST API with a token read from ``GITHUB_TOKEN`` (or
``GH_TOKEN``). The token needs ``contents:write`` + ``pull_requests:write`` to
open PRs and ``actions:read``/``write`` to dispatch and read workflow runs. If no
token is present every tool returns a clear, structured error instead of raising,
so a misconfiguration never crashes the agent.

The target repository defaults to ``Azure/azure-sdk-tools`` and is overridable
via ``GITHUB_REPO`` (``owner/name``).
"""

from __future__ import annotations

import base64
import io
import json
import logging
import os
import time
import zipfile
from typing import Annotated, Any, Optional

import httpx

logger = logging.getLogger(__name__)

_API_ROOT = "https://api.github.com"
_TIMEOUT_SECONDS = 30
_DEFAULT_REPO = "Azure/azure-sdk-tools"
_MAX_TEXT_CHARS = 100_000

# Records the result dict of the most recent successful open_skill_pull_request
# call so the coded workflow (workflow.py) can pick up the draft PR number/branch
# after an agent turn opened it. Appended on success; the last entry is current.
LAST_PR_RESULTS: list[dict[str, Any]] = []

# Records the most recent successful push_skill_changes (commit-to-branch, no PR)
# so the gated workflow can pick up the branch to benchmark before opening a PR.
LAST_BRANCH_PUSHES: list[dict[str, Any]] = []


def _repo() -> str:
    return os.environ.get("GITHUB_REPO", _DEFAULT_REPO).strip().strip("/")


def comment_on_pr(pr_number: int, body: str) -> dict[str, Any]:
    """Post a comment on a pull request (used by workflow step 5 to attach the
    gap report to the draft PR). Returns a result dict."""
    token = _token()
    if not token:
        return {"status": "error", "message": "No GitHub credentials."}
    owner_repo = _repo()
    url = f"{_API_ROOT}/repos/{owner_repo}/issues/{pr_number}/comments"
    try:
        with httpx.Client(timeout=_TIMEOUT_SECONDS, headers=_headers(token)) as client:
            resp = client.post(url, json={"body": body[:60_000]})
        if resp.status_code != 201:
            return {"status": "error", "message": f"comment failed {resp.status_code}: {resp.text[:300]}"}
        return {"status": "ok", "comment_url": resp.json().get("html_url", "")}
    except Exception as exc:  # pragma: no cover
        return {"status": "error", "message": f"{type(exc).__name__}: {str(exc)[:300]}"}


# ----------------------------------------------------------------------------
# Authentication: a Personal Access Token OR a GitHub App installation token.
#
#   PAT mode (simple/manual): set GITHUB_TOKEN (or GH_TOKEN).
#   GitHub App mode (recommended for automation — no personal identity, scoped
#   install permissions, short-lived 1h tokens): set
#       GITHUB_APP_ID
#       GITHUB_APP_INSTALLATION_ID
#       GITHUB_APP_PRIVATE_KEY          (PEM text)  -- or --
#       GITHUB_APP_PRIVATE_KEY_BASE64   (base64 of the PEM, avoids newline issues)
#   The installation token is minted on demand from a signed JWT and cached
#   until ~5 minutes before it expires. A PAT, if present, takes precedence.
# ----------------------------------------------------------------------------

_APP_TOKEN_CACHE: dict[str, Any] = {"token": None, "expires_at": 0.0}


def _app_private_key() -> Optional[str]:
    pem = os.environ.get("GITHUB_APP_PRIVATE_KEY")
    if pem and pem.strip():
        return pem
    b64 = os.environ.get("GITHUB_APP_PRIVATE_KEY_BASE64")
    if b64 and b64.strip():
        try:
            return base64.b64decode(b64).decode("utf-8")
        except Exception:  # pragma: no cover
            logger.warning("GITHUB_APP_PRIVATE_KEY_BASE64 is not valid base64.")
            return None
    return None


def _mint_installation_token() -> Optional[str]:
    """Mint (and cache) a GitHub App installation access token via a signed JWT.

    Returns ``None`` when the App is not configured or minting fails, so callers
    fall back to the standard "no credentials" guard rather than crashing.
    """
    app_id = os.environ.get("GITHUB_APP_ID")
    installation_id = os.environ.get("GITHUB_APP_INSTALLATION_ID")
    private_key = _app_private_key()
    if not (app_id and installation_id and private_key):
        return None

    now = time.time()
    cached = _APP_TOKEN_CACHE.get("token")
    if cached and _APP_TOKEN_CACHE.get("expires_at", 0.0) - 300 > now:
        return cached

    try:
        import jwt  # PyJWT[crypto]
    except Exception:  # pragma: no cover
        logger.warning("PyJWT is not installed; cannot use GitHub App auth.")
        return None

    # App JWT: must be short-lived (<=10 min) and signed RS256 with the App key.
    payload = {"iat": int(now) - 60, "exp": int(now) + 540, "iss": str(app_id)}
    try:
        app_jwt = jwt.encode(payload, private_key, algorithm="RS256")
    except Exception as exc:  # pragma: no cover
        logger.warning("Failed to sign the GitHub App JWT: %s", exc)
        return None

    try:
        with httpx.Client(timeout=_TIMEOUT_SECONDS) as client:
            resp = client.post(
                f"{_API_ROOT}/app/installations/{installation_id}/access_tokens",
                headers={
                    "Authorization": f"Bearer {app_jwt}",
                    "Accept": "application/vnd.github+json",
                    "X-GitHub-Api-Version": "2022-11-28",
                    "User-Agent": "typespec-skill-self-evolving-agent/1.0",
                },
            )
        if resp.status_code != 201:
            logger.warning(
                "installation token exchange failed %s: %s", resp.status_code, resp.text[:300]
            )
            return None
        token = resp.json().get("token")
        # Token lives ~1h; cache conservatively for 50 min.
        _APP_TOKEN_CACHE["token"] = token
        _APP_TOKEN_CACHE["expires_at"] = now + 3000
        return token
    except Exception as exc:  # pragma: no cover
        logger.warning("installation token request error: %s", exc)
        return None


def _token() -> Optional[str]:
    pat = os.environ.get("GITHUB_TOKEN") or os.environ.get("GH_TOKEN")
    if pat:
        return pat
    return _mint_installation_token()


def _headers(token: str) -> dict[str, str]:
    return {
        "Authorization": f"Bearer {token}",
        "Accept": "application/vnd.github+json",
        "X-GitHub-Api-Version": "2022-11-28",
        "User-Agent": "typespec-skill-self-evolving-agent/1.0",
    }


def _err(message: str, **extra: Any) -> str:
    return json.dumps({"status": "error", "message": message, **extra}, ensure_ascii=False)


def _need_token() -> Optional[str]:
    if not _token():
        return _err(
            "No GitHub credentials configured. Set GITHUB_TOKEN (a PAT) OR configure "
            "a GitHub App (GITHUB_APP_ID + GITHUB_APP_INSTALLATION_ID + "
            "GITHUB_APP_PRIVATE_KEY or GITHUB_APP_PRIVATE_KEY_BASE64) at deploy time "
            "so the agent can read the repo, open PRs, and read CI runs remotely."
        )
    return None


def read_repo_file(
    path: Annotated[str, "Repo-relative file path, e.g. '.github/skills/azure-typespec-author/SKILL.md'."],
    ref: Annotated[str, "Branch, tag, or commit SHA to read from."] = "main",
) -> str:
    """Read a single file from the GitHub repository and return its text.

    Use this in Step 1 to inspect the current skill surface (``SKILL.md`` and its
    ``references/*.md``) and the benchmark suites under ``evaluate/`` without a
    local checkout. Returns a JSON object with ``path``, ``ref``, ``sha`` (needed
    to update the file later), and ``content``.
    """
    guard = _need_token()
    if guard:
        return guard
    owner_repo = _repo()
    url = f"{_API_ROOT}/repos/{owner_repo}/contents/{path.lstrip('/')}"
    try:
        with httpx.Client(timeout=_TIMEOUT_SECONDS) as client:
            resp = client.get(url, headers=_headers(_token()), params={"ref": ref})
        if resp.status_code == 404:
            return _err(f"File not found: {path} @ {ref}", path=path, ref=ref)
        resp.raise_for_status()
        data = resp.json()
        if isinstance(data, list):
            return _err(f"Path is a directory, use list_repo_dir: {path}", path=path)
        raw = base64.b64decode(data.get("content", "")).decode("utf-8", errors="replace")
        return json.dumps(
            {
                "status": "ok",
                "path": path,
                "ref": ref,
                "sha": data.get("sha"),
                "content": raw[:_MAX_TEXT_CHARS],
            },
            ensure_ascii=False,
        )
    except httpx.HTTPStatusError as exc:
        return _err(f"GitHub API error {exc.response.status_code}: {exc.response.text[:300]}")
    except Exception as exc:  # pragma: no cover
        return _err(f"{type(exc).__name__}: {str(exc)[:300]}")


def list_repo_dir(
    path: Annotated[str, "Repo-relative directory path, e.g. '.github/skills/azure-typespec-author/references'."],
    ref: Annotated[str, "Branch, tag, or commit SHA to list from."] = "main",
) -> str:
    """List the entries of a directory in the GitHub repository.

    Use this in Step 1 to discover the skill's ``references/`` files and the
    available benchmark suites under ``evaluate/`` before deciding what to edit.
    Returns a JSON list of ``{name, path, type}``.
    """
    guard = _need_token()
    if guard:
        return guard
    owner_repo = _repo()
    url = f"{_API_ROOT}/repos/{owner_repo}/contents/{path.lstrip('/')}"
    try:
        with httpx.Client(timeout=_TIMEOUT_SECONDS) as client:
            resp = client.get(url, headers=_headers(_token()), params={"ref": ref})
        if resp.status_code == 404:
            return _err(f"Directory not found: {path} @ {ref}", path=path, ref=ref)
        resp.raise_for_status()
        data = resp.json()
        if not isinstance(data, list):
            return _err(f"Path is a file, use read_repo_file: {path}", path=path)
        entries = [{"name": e["name"], "path": e["path"], "type": e["type"]} for e in data]
        return json.dumps({"status": "ok", "path": path, "ref": ref, "entries": entries}, ensure_ascii=False)
    except httpx.HTTPStatusError as exc:
        return _err(f"GitHub API error {exc.response.status_code}: {exc.response.text[:300]}")
    except Exception as exc:  # pragma: no cover
        return _err(f"{type(exc).__name__}: {str(exc)[:300]}")


def _commit_files_to_branch(client, owner_repo: str, branch: str, base: str, files: list) -> list[str]:
    """Create *branch* off *base* (idempotent) and commit each file. Returns the
    list of committed paths. Raises on GitHub API errors."""
    # 1. Resolve the base branch head SHA.
    r = client.get(f"{_API_ROOT}/repos/{owner_repo}/git/ref/heads/{base}")
    if r.status_code == 404:
        raise ValueError(f"Base branch not found: {base}")
    r.raise_for_status()
    base_sha = r.json()["object"]["sha"]

    # 2. Create the head branch (idempotent: ignore 'already exists' = 422).
    r = client.post(
        f"{_API_ROOT}/repos/{owner_repo}/git/refs",
        json={"ref": f"refs/heads/{branch}", "sha": base_sha},
    )
    if r.status_code not in (201, 422):
        r.raise_for_status()

    # 3. Commit each file via the contents API (create or update).
    committed = []
    for entry in files:
        path = str(entry["path"]).lstrip("/")
        content_b64 = base64.b64encode(str(entry["content"]).encode("utf-8")).decode("ascii")
        sha = None
        g = client.get(
            f"{_API_ROOT}/repos/{owner_repo}/contents/{path}",
            params={"ref": branch},
        )
        if g.status_code == 200 and isinstance(g.json(), dict):
            sha = g.json().get("sha")
        payload = {
            "message": f"self-evolve: update {path}",
            "content": content_b64,
            "branch": branch,
        }
        if sha:
            payload["sha"] = sha
        p = client.put(f"{_API_ROOT}/repos/{owner_repo}/contents/{path}", json=payload)
        p.raise_for_status()
        committed.append(path)
    return committed


def push_skill_changes(
    branch: Annotated[str, "New head branch to create and commit onto, e.g. 'self-evolve/paging-topskip'."],
    files_json: Annotated[
        str,
        "JSON array of files to write, each {\"path\": str, \"content\": str}. Full new file content.",
    ],
    base: Annotated[str, "Base branch to branch from and (later) target the PR at."] = "main",
) -> str:
    """Commit skill/reference changes to a **branch without opening a PR**.

    Use this in the gated workflow: the agent pushes its Step-1 edits to a branch,
    the benchmark runs against that branch, and a **draft** PR is opened later with
    ``open_draft_pr`` only if the benchmark clears the pass-rate threshold. Returns
    the ``branch`` and the list of ``files_committed``.
    """
    guard = _need_token()
    if guard:
        return guard
    try:
        files = json.loads(files_json)
        if not isinstance(files, list) or not files:
            return _err("files_json must be a non-empty JSON array of {path, content}.")
    except json.JSONDecodeError as exc:
        return _err(f"files_json is not valid JSON: {exc}")

    owner_repo = _repo()
    try:
        with httpx.Client(timeout=_TIMEOUT_SECONDS, headers=_headers(_token())) as client:
            committed = _commit_files_to_branch(client, owner_repo, branch, base, files)
        result = {"status": "ok", "branch": branch, "base": base, "files_committed": committed}
        LAST_BRANCH_PUSHES.append(result)
        return json.dumps(result, ensure_ascii=False)
    except ValueError as exc:
        return _err(str(exc))
    except httpx.HTTPStatusError as exc:
        return _err(f"GitHub API error {exc.response.status_code}: {exc.response.text[:400]}")
    except Exception as exc:  # pragma: no cover
        return _err(f"{type(exc).__name__}: {str(exc)[:300]}")


def open_draft_pr(
    branch: Annotated[str, "Existing head branch (already pushed via push_skill_changes)."],
    title: Annotated[str, "Pull request title."],
    body: Annotated[str, "Pull request body: include the benchmark test report."],
    base: Annotated[str, "Base branch to target."] = "main",
) -> str:
    """Open a **draft** pull request from an already-pushed *branch*.

    Draft is forced (not a caller flag): the self-evolving agent never opens a
    ready-for-review PR and never merges — a human promotes it out of draft after
    reviewing the evidence. Returns the PR ``number``, ``html_url``, and ``draft``.
    """
    guard = _need_token()
    if guard:
        return guard
    owner_repo = _repo()
    try:
        with httpx.Client(timeout=_TIMEOUT_SECONDS, headers=_headers(_token())) as client:
            pr = client.post(
                f"{_API_ROOT}/repos/{owner_repo}/pulls",
                json={"title": title, "head": branch, "base": base, "body": body, "draft": True},
            )
            pr.raise_for_status()
            pr_data = pr.json()
        result = {
            "status": "ok",
            "draft": pr_data.get("draft", True),
            "pr_number": pr_data["number"],
            "html_url": pr_data["html_url"],
            "branch": branch,
        }
        LAST_PR_RESULTS.append(result)
        return json.dumps(result, ensure_ascii=False)
    except httpx.HTTPStatusError as exc:
        return _err(f"GitHub API error {exc.response.status_code}: {exc.response.text[:400]}")
    except Exception as exc:  # pragma: no cover
        return _err(f"{type(exc).__name__}: {str(exc)[:300]}")


def open_skill_pull_request(
    branch: Annotated[str, "New head branch name to create, e.g. 'self-evolve/clarify-arm-extension'."],
    title: Annotated[str, "Pull request title."],
    body: Annotated[str, "Pull request body: include the benchmark test report."],
    files_json: Annotated[
        str,
        "JSON array of files to write, each {\"path\": str, \"content\": str}. Full new file content.",
    ],
    base: Annotated[str, "Base branch to target and branch from."] = "main",
) -> str:
    """Create a branch, commit one or more file changes, and open a **draft** pull request.

    Convenience wrapper that commits (``push_skill_changes``) and immediately opens
    the draft PR (``open_draft_pr``) in one call. In the **gated** workflow prefer
    calling those two separately so the benchmark can run between them. The PR is
    always a **draft** — the agent never opens a ready-for-review PR and never
    merges. Returns the PR ``number``, ``html_url``, and ``draft`` flag.
    """
    guard = _need_token()
    if guard:
        return guard
    try:
        files = json.loads(files_json)
        if not isinstance(files, list) or not files:
            return _err("files_json must be a non-empty JSON array of {path, content}.")
    except json.JSONDecodeError as exc:
        return _err(f"files_json is not valid JSON: {exc}")

    owner_repo = _repo()
    token = _token()
    headers = _headers(token)
    try:
        with httpx.Client(timeout=_TIMEOUT_SECONDS, headers=headers) as client:
            committed = _commit_files_to_branch(client, owner_repo, branch, base, files)
            # Open the pull request as a DRAFT. Intentionally forced (not a
            # caller-controllable flag): the self-evolving agent must never open a
            # ready-for-review PR — a human promotes it out of draft.
            pr = client.post(
                f"{_API_ROOT}/repos/{owner_repo}/pulls",
                json={
                    "title": title,
                    "head": branch,
                    "base": base,
                    "body": body,
                    "draft": True,
                },
            )
            pr.raise_for_status()
            pr_data = pr.json()
        result = {
            "status": "ok",
            "draft": pr_data.get("draft", True),
            "pr_number": pr_data["number"],
            "html_url": pr_data["html_url"],
            "branch": branch,
            "files_committed": committed,
        }
        LAST_PR_RESULTS.append(result)
        return json.dumps(result, ensure_ascii=False)
    except ValueError as exc:
        return _err(str(exc))
    except httpx.HTTPStatusError as exc:
        return _err(f"GitHub API error {exc.response.status_code}: {exc.response.text[:400]}")
    except Exception as exc:  # pragma: no cover
        return _err(f"{type(exc).__name__}: {str(exc)[:300]}")


def dispatch_workflow(
    ref: Annotated[str, "Branch or tag to run the workflow on, e.g. the PR head branch."],
    workflow_file: Annotated[str, "Workflow file name under .github/workflows/, e.g. 'skill-eval.yml'."] = "skill-eval.yml",
    inputs_json: Annotated[str, "Optional JSON object of workflow_dispatch inputs."] = "{}",
) -> str:
    """Trigger a GitHub Actions workflow via ``workflow_dispatch`` (Step 2).

    Use this to run the skill benchmark remotely — the agent cannot run ``vally``
    in its container. ``skill-eval.yml`` also auto-runs when a PR touches
    ``.github/skills/**``, so an explicit dispatch is only needed for a re-run or
    a non-PR ref. Returns a receipt; poll status with ``get_latest_workflow_run``.
    """
    guard = _need_token()
    if guard:
        return guard
    try:
        inputs = json.loads(inputs_json) if inputs_json.strip() else {}
    except json.JSONDecodeError as exc:
        return _err(f"inputs_json is not valid JSON: {exc}")
    owner_repo = _repo()
    url = f"{_API_ROOT}/repos/{owner_repo}/actions/workflows/{workflow_file}/dispatches"
    try:
        with httpx.Client(timeout=_TIMEOUT_SECONDS) as client:
            resp = client.post(url, headers=_headers(_token()), json={"ref": ref, "inputs": inputs})
        if resp.status_code == 204:
            return json.dumps(
                {"status": "ok", "dispatched": workflow_file, "ref": ref, "inputs": inputs},
                ensure_ascii=False,
            )
        return _err(f"dispatch failed {resp.status_code}: {resp.text[:300]}")
    except Exception as exc:  # pragma: no cover
        return _err(f"{type(exc).__name__}: {str(exc)[:300]}")


def get_latest_workflow_run(
    branch: Annotated[str, "Branch to read the latest run for (e.g. the PR head branch)."],
    workflow_file: Annotated[str, "Workflow file name, e.g. 'skill-eval.yml'."] = "skill-eval.yml",
) -> str:
    """Return the latest workflow run for a branch: status + conclusion + URL.

    Use this in Steps 2/3 to wait for the benchmark CI and read its result. Poll
    until ``status == 'completed'``, then read ``conclusion`` ('success' /
    'failure') and, if the workflow uploads results, ``download_workflow_artifact``.
    """
    guard = _need_token()
    if guard:
        return guard
    owner_repo = _repo()
    url = f"{_API_ROOT}/repos/{owner_repo}/actions/workflows/{workflow_file}/runs"
    try:
        with httpx.Client(timeout=_TIMEOUT_SECONDS) as client:
            resp = client.get(
                url,
                headers=_headers(_token()),
                params={"branch": branch, "per_page": 1},
            )
        resp.raise_for_status()
        runs = resp.json().get("workflow_runs", [])
        if not runs:
            return json.dumps(
                {"status": "ok", "found": False, "message": f"No runs yet for {workflow_file} on {branch}."},
                ensure_ascii=False,
            )
        run = runs[0]
        return json.dumps(
            {
                "status": "ok",
                "found": True,
                "run_id": run["id"],
                "run_status": run["status"],
                "conclusion": run.get("conclusion"),
                "html_url": run["html_url"],
                "head_sha": run.get("head_sha"),
            },
            ensure_ascii=False,
        )
    except httpx.HTTPStatusError as exc:
        return _err(f"GitHub API error {exc.response.status_code}: {exc.response.text[:300]}")
    except Exception as exc:  # pragma: no cover
        return _err(f"{type(exc).__name__}: {str(exc)[:300]}")


def download_workflow_artifact(
    run_id: Annotated[int, "The workflow run id from get_latest_workflow_run."],
    artifact_name: Annotated[str, "Name of the artifact to download, e.g. 'vally-results'."],
    inner_file: Annotated[
        Optional[str],
        "Optional file name inside the artifact zip to return (e.g. 'results.jsonl'). If omitted, returns the first text file.",
    ] = None,
) -> str:
    """Download a workflow run artifact and return the text of a file inside it.

    Use this in Step 3 to fetch the benchmark's ``results.jsonl`` when the eval
    workflow uploads one, then feed the returned content into
    ``summarize_benchmark_results``. Returns a JSON object with ``filename`` and
    ``content``.
    """
    guard = _need_token()
    if guard:
        return guard
    owner_repo = _repo()
    try:
        with httpx.Client(timeout=_TIMEOUT_SECONDS, headers=_headers(_token()), follow_redirects=True) as client:
            listing = client.get(f"{_API_ROOT}/repos/{owner_repo}/actions/runs/{run_id}/artifacts")
            listing.raise_for_status()
            match = next(
                (a for a in listing.json().get("artifacts", []) if a["name"] == artifact_name),
                None,
            )
            if match is None:
                names = [a["name"] for a in listing.json().get("artifacts", [])]
                return _err(f"Artifact '{artifact_name}' not found. Available: {names}", run_id=run_id)
            dl = client.get(match["archive_download_url"])
            dl.raise_for_status()
            with zipfile.ZipFile(io.BytesIO(dl.content)) as zf:
                name = inner_file or next((n for n in zf.namelist() if not n.endswith("/")), None)
                if name is None:
                    return _err("Artifact zip is empty.", run_id=run_id)
                text = zf.read(name).decode("utf-8", errors="replace")
        return json.dumps(
            {"status": "ok", "filename": name, "content": text[:_MAX_TEXT_CHARS]},
            ensure_ascii=False,
        )
    except httpx.HTTPStatusError as exc:
        return _err(f"GitHub API error {exc.response.status_code}: {exc.response.text[:300]}")
    except Exception as exc:  # pragma: no cover
        return _err(f"{type(exc).__name__}: {str(exc)[:300]}")
