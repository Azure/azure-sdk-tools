from __future__ import annotations

import os
import re
import subprocess
from collections.abc import Callable, Sequence
from dataclasses import dataclass
from pathlib import Path, PurePosixPath, PureWindowsPath

from .models import RepositoryConfig, RepositorySnapshot, SubmoduleSnapshot
from .repositories import canonical_git_url

CommandRunner = Callable[[Sequence[str], Path | None], str]

_SUBMODULE_STATUS = re.compile(
    r"^(?P<marker>[ +\-U])(?P<commit>[0-9a-fA-F]{40,64}) "
    r"(?P<path>.+?)(?: \(.+\))?$"
)
_GITLINK = re.compile(r"^160000 commit ([0-9a-fA-F]{40,64})\t")


@dataclass(frozen=True, slots=True)
class ParsedSubmoduleStatus:
    marker: str
    commit: str
    path: str


def checkout_repository(
    config: RepositoryConfig,
    destination: Path,
    *,
    runner: CommandRunner | None = None,
) -> RepositorySnapshot:
    """Create a new depth-one detached checkout and initialize submodules."""
    run = runner or _run
    if destination.exists():
        raise RuntimeError(f"checkout destination already exists: {destination}")
    destination.mkdir(parents=True)

    run(("git", "init", "--quiet"), destination)
    run(("git", "remote", "add", "origin", config.git_url), destination)
    run(
        (
            "git",
            "fetch",
            "--quiet",
            "--force",
            "--no-tags",
            "--depth=1",
            "origin",
            config.git_ref,
        ),
        destination,
    )
    commit = run(("git", "rev-parse", "FETCH_HEAD^{commit}"), destination).strip()
    _validate_commit(commit, "resolved repository commit")
    run(
        ("git", "checkout", "--quiet", "--detach", "--force", commit),
        destination,
    )
    actual = run(("git", "rev-parse", "HEAD"), destination).strip()
    if actual != commit:
        raise RuntimeError(
            f"detached checkout mismatch: expected {commit}, got {actual}"
        )
    _verify_depth_one(destination, run)

    run(("git", "submodule", "sync", "--recursive"), destination)
    run(
        (
            "git",
            "submodule",
            "update",
            "--init",
            "--recursive",
            "--depth=1",
            "--recommend-shallow",
        ),
        destination,
    )
    status_output = run(
        ("git", "submodule", "status", "--recursive"), destination
    )
    submodules = validate_submodules(
        destination,
        parse_submodule_status(status_output),
        runner=run,
    )
    return RepositorySnapshot(
        root=destination,
        commit=commit,
        submodules=submodules,
    )


def parse_submodule_status(output: str) -> tuple[ParsedSubmoduleStatus, ...]:
    statuses: list[ParsedSubmoduleStatus] = []
    paths: set[str] = set()
    for line in output.splitlines():
        if not line:
            continue
        match = _SUBMODULE_STATUS.fullmatch(line)
        if match is None:
            raise RuntimeError(f"Unrecognized git submodule status line: {line!r}")
        path = _normalize_submodule_path(match.group("path"))
        if path in paths:
            raise RuntimeError(f"Duplicate recursive submodule path: {path}")
        paths.add(path)
        statuses.append(
            ParsedSubmoduleStatus(
                marker=match.group("marker"),
                commit=match.group("commit").lower(),
                path=path,
            )
        )
    return tuple(sorted(statuses, key=lambda item: item.path))


def validate_submodules(
    root: Path,
    statuses: tuple[ParsedSubmoduleStatus, ...],
    *,
    runner: CommandRunner | None = None,
) -> tuple[SubmoduleSnapshot, ...]:
    """Verify every recursive submodule is initialized at its gitlink commit."""
    run = runner or _run
    known_paths: list[str] = []
    snapshots: list[SubmoduleSnapshot] = []

    for status in sorted(statuses, key=lambda item: (item.path.count("/"), item.path)):
        if status.marker != " ":
            meaning = {
                "-": "not initialized",
                "+": "checked out at a different commit",
                "U": "in a merge-conflict state",
            }.get(status.marker, "invalid")
            raise RuntimeError(f"Submodule {status.path!r} is {meaning}")

        submodule_root = _safe_submodule_root(root, status.path)
        if not submodule_root.is_dir():
            raise RuntimeError(
                f"Initialized submodule directory is missing: {status.path}"
            )
        actual = run(("git", "rev-parse", "HEAD"), submodule_root).strip().lower()
        _validate_commit(actual, f"submodule {status.path} commit")

        parent_path = _nearest_parent(status.path, known_paths)
        parent_root = root / Path(*PurePosixPath(parent_path).parts) if parent_path else root
        relative_path = (
            status.path[len(parent_path) + 1 :] if parent_path else status.path
        )
        tree_entry = run(
            ("git", "ls-tree", "HEAD", "--", relative_path), parent_root
        ).strip()
        match = _GITLINK.match(tree_entry)
        if match is None:
            raise RuntimeError(
                f"Submodule {status.path!r} has no commit gitlink in its parent"
            )
        expected = match.group(1).lower()
        if status.commit != expected or actual != expected:
            raise RuntimeError(
                f"Submodule {status.path!r} commit mismatch: gitlink {expected}, "
                f"status {status.commit}, checkout {actual}"
            )
        _verify_depth_one(submodule_root, run)

        raw_url = run(
            ("git", "remote", "get-url", "origin"), submodule_root
        ).strip()
        git_url, _, _ = canonical_git_url(raw_url)
        snapshots.append(
            SubmoduleSnapshot(
                path=status.path,
                git_url=git_url,
                commit=actual,
            )
        )
        known_paths.append(status.path)

    return tuple(sorted(snapshots, key=lambda item: item.path))


def _nearest_parent(path: str, candidates: list[str]) -> str:
    parents = [
        candidate
        for candidate in candidates
        if path.startswith(f"{candidate}/")
    ]
    return max(parents, key=len, default="")


def _safe_submodule_root(root: Path, path: str) -> Path:
    candidate = root
    for part in PurePosixPath(path).parts:
        candidate = candidate / part
        if candidate.is_symlink():
            raise RuntimeError(f"Submodule path contains a symlink: {path}")
    return candidate


def _normalize_submodule_path(value: str) -> str:
    raw = value.strip()
    if (
        "\\" in raw
        or raw.startswith("/")
        or PureWindowsPath(raw).is_absolute()
    ):
        raise RuntimeError(f"Unsafe submodule path: {value!r}")
    raw = raw.rstrip("/")
    path = PurePosixPath(raw)
    if not raw or path.is_absolute() or ".." in path.parts or "." in path.parts:
        raise RuntimeError(f"Unsafe submodule path: {value!r}")
    return path.as_posix()


def _verify_depth_one(root: Path, run: CommandRunner) -> None:
    count = run(("git", "rev-list", "--count", "HEAD"), root).strip()
    if count != "1":
        raise RuntimeError(
            f"Checkout at {root} retained {count} commits instead of one"
        )


def _validate_commit(value: str, label: str) -> None:
    if re.fullmatch(r"[0-9a-fA-F]{40,64}", value) is None:
        raise RuntimeError(f"Invalid {label}: {value!r}")


def _run(args: Sequence[str], cwd: Path | None) -> str:
    environment = {
        **os.environ,
        "GCM_INTERACTIVE": "Never",
        "GIT_ALLOW_PROTOCOL": "https",
        "GIT_TERMINAL_PROMPT": "0",
    }
    try:
        completed = subprocess.run(
            list(args),
            cwd=cwd,
            env=environment,
            check=False,
            capture_output=True,
            text=True,
            encoding="utf-8",
            timeout=600,
        )
    except subprocess.TimeoutExpired as exc:
        raise RuntimeError(
            f"{' '.join(args)} timed out after 600 seconds"
        ) from exc
    if completed.returncode:
        detail = completed.stderr.strip() or completed.stdout.strip()
        raise RuntimeError(
            f"{' '.join(args)} failed ({completed.returncode}): {detail}"
        )
    return completed.stdout
