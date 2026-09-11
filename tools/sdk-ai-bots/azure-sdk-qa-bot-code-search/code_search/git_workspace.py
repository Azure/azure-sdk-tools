from __future__ import annotations

import hashlib
import logging
import shutil
import subprocess
from dataclasses import dataclass
from pathlib import Path

from .models import RepositoryIndexConfig
from .plan import canonical_git_url

logger = logging.getLogger(__name__)


@dataclass(frozen=True, slots=True)
class GitSnapshot:
    workspace: Path
    commit_sha: str
    tree_sha: str
    cache_sha256: str


def resolve_ref(git_url: str, git_ref: str) -> str:
    output = _run(
        ["git", "ls-remote", "--exit-code", git_url, git_ref, f"{git_ref}^{{}}"],
        cwd=None,
    )
    matches = {
        ref: sha
        for line in output.splitlines()
        if line.strip()
        for sha, ref in [line.split(maxsplit=1)]
    }
    resolved = matches.get(f"{git_ref}^{{}}") or matches.get(git_ref)
    if resolved is None or len(resolved) != 40:
        raise RuntimeError(
            f"git ls-remote did not resolve a commit for {git_url} {git_ref}"
        )
    return resolved


def prepare_workspace(
    config: RepositoryIndexConfig, cache_root: Path, commit_sha: str
) -> GitSnapshot:
    workspace = cache_root / "repositories" / config.repository_key
    workspace.parent.mkdir(parents=True, exist_ok=True)
    if not _valid_workspace(workspace, config.git_url):
        if workspace.exists():
            shutil.rmtree(workspace)
        workspace.mkdir(parents=True)
        _run(["git", "init", "--quiet"], cwd=workspace)
        _run(["git", "remote", "add", "origin", config.git_url], cwd=workspace)

    _run(
        [
            "git",
            "fetch",
            "--quiet",
            "--force",
            "--no-tags",
            "--depth=1",
            "origin",
            config.git_ref,
        ],
        cwd=workspace,
    )
    _run(["git", "checkout", "--quiet", "--detach", "--force", commit_sha], cwd=workspace)
    _run(["git", "clean", "-ffd", "-x"], cwd=workspace)
    actual_commit = _run(["git", "rev-parse", "HEAD"], cwd=workspace).strip()
    if actual_commit != commit_sha:
        raise RuntimeError(
            f"detached checkout mismatch: expected {commit_sha}, got {actual_commit}"
        )
    tree_sha = _run(["git", "rev-parse", "HEAD^{tree}"], cwd=workspace).strip()
    cache_hash = hashlib.sha256(
        f"{config.git_url}\0{config.git_ref}\0{commit_sha}".encode("utf-8")
    ).hexdigest()
    logger.info("prepared depth-one checkout %s at %s", commit_sha, workspace)
    return GitSnapshot(workspace, commit_sha, tree_sha, cache_hash)


def _valid_workspace(workspace: Path, git_url: str) -> bool:
    if not (workspace / ".git").is_dir():
        return False
    try:
        origin = _run(["git", "remote", "get-url", "origin"], cwd=workspace).strip()
        return canonical_git_url(origin) == git_url
    except (RuntimeError, ValueError):
        return False


def _run(args: list[str], cwd: Path | None) -> str:
    completed = subprocess.run(
        args,
        cwd=cwd,
        check=False,
        capture_output=True,
        text=True,
        encoding="utf-8",
    )
    if completed.returncode:
        detail = completed.stderr.strip() or completed.stdout.strip()
        raise RuntimeError(f"{' '.join(args)} failed ({completed.returncode}): {detail}")
    return completed.stdout
