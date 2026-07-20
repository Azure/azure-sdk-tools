"""Validate that a GitHub token has the permissions the Self-Evolving Agent needs.

The hosted agent, in remote mode, must be able to:
  * read the repo               (Contents: read)   -> read_repo_file / list_repo_dir
  * open pull requests          (Contents: write + Pull requests: write)
  * dispatch + read the CI      (Actions: read/write) -> dispatch_workflow / get_latest_workflow_run

This script exercises the *read* paths for real and checks the token's declared
scopes/permissions so you can confirm a token BEFORE injecting it at deploy time.
It never prints the token.

Usage (PowerShell):
    $env:AGENT_GH_PAT = "<paste token>"
    python validate_github_access.py            # defaults to Azure/azure-sdk-tools
    python validate_github_access.py --repo owner/name
"""

from __future__ import annotations

import argparse
import os
import sys

import httpx

_API = "https://api.github.com"


def _headers(token: str) -> dict[str, str]:
    return {
        "Authorization": f"Bearer {token}",
        "Accept": "application/vnd.github+json",
        "X-GitHub-Api-Version": "2022-11-28",
        "User-Agent": "typespec-skill-self-evolving-agent-validator/1.0",
    }


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--repo", default=os.environ.get("GITHUB_REPO", "Azure/azure-sdk-tools"))
    args = ap.parse_args()

    token = os.environ.get("AGENT_GH_PAT") or os.environ.get("GITHUB_TOKEN") or os.environ.get("GH_TOKEN")
    if not token:
        print("ERROR: set AGENT_GH_PAT (or GITHUB_TOKEN) to the token to validate.")
        return 2

    repo = args.repo.strip().strip("/")
    ok = True
    with httpx.Client(timeout=30, headers=_headers(token)) as client:
        # 1. Identity + classic scopes (empty for fine-grained tokens).
        who = client.get(f"{_API}/user")
        login = who.json().get("login", "?") if who.status_code == 200 else "?"
        scopes = who.headers.get("x-oauth-scopes", "")
        print(f"authenticated as: {login}")
        print(f"classic scopes:   {scopes or '(fine-grained token — permissions are per-resource)'}")

        # 2. Repo read + declared permissions.
        r = client.get(f"{_API}/repos/{repo}")
        if r.status_code != 200:
            print(f"FAIL  repo read {repo}: HTTP {r.status_code} {r.text[:120]}")
            return 1
        perms = r.json().get("permissions", {})
        print(f"repo:             {repo}")
        print(f"repo permissions: push={perms.get('push')} admin={perms.get('admin')}")

        # 3. Contents read (a real file the agent will read).
        c = client.get(
            f"{_API}/repos/{repo}/contents/.github/skills/azure-typespec-author/SKILL.md",
            params={"ref": "main"},
        )
        print(f"[{'PASS' if c.status_code == 200 else 'FAIL'}] Contents: read  (read SKILL.md) -> HTTP {c.status_code}")
        ok = ok and c.status_code == 200

        # 4. Pull requests read (list is a cheap proxy; write is exercised at PR-open time).
        p = client.get(f"{_API}/repos/{repo}/pulls", params={"state": "open", "per_page": 1})
        print(f"[{'PASS' if p.status_code == 200 else 'FAIL'}] Pull requests: read -> HTTP {p.status_code}")
        ok = ok and p.status_code == 200

        # 5. Actions read (list workflows / runs).
        a = client.get(f"{_API}/repos/{repo}/actions/workflows", params={"per_page": 1})
        print(f"[{'PASS' if a.status_code == 200 else 'FAIL'}] Actions: read -> HTTP {a.status_code}")
        ok = ok and a.status_code == 200

    print()
    if ok:
        print("RESULT: token can read the repo, PRs, and Actions. Contents:write / "
              "Pull requests:write / Actions:write are required too — confirm those are "
              "granted (fine-grained) or that 'repo'+'workflow' scopes are present (classic).")
        print("Next: deploy with  --github-token \"$env:AGENT_GH_PAT\"")
        return 0
    print("RESULT: one or more read checks failed — the token lacks access to this repo.")
    return 1


if __name__ == "__main__":
    sys.exit(main())
