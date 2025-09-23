"""Workflow configuration loader for evals.

Minimal schema (intentionally lean):
- name: unique workflow name
- kind: 'prompt' | 'apiview'
- tests: path to .jsonl testcases (relative to yaml location allowed)
- prompty: required iff kind == 'prompt'

Future (not implemented yet): metrics, model, post_process, baseline, coverage.
"""
from __future__ import annotations

import dataclasses
import os
from pathlib import Path
from typing import List
from evals._custom import EVALUATORS
import yaml

SUPPORTED_WORKFLOWS = set(EVALUATORS.keys())

@dataclasses.dataclass(slots=True)
class WorkflowConfig:
    name: str
    kind: str
    tests_path: Path
    prompty_path: Path | None
    source_file: Path | None = None  # for diagnostics

class WorkflowConfigError(ValueError):
    pass


def _fail(msg: str) -> None:
    raise WorkflowConfigError(msg)


def load_workflow_config(path: str | os.PathLike) -> WorkflowConfig:
    """Load and validate a single workflow yaml file.

    Args:
        path: Path to YAML file.
    Returns:
        WorkflowConfig
    Raises:
        WorkflowConfigError for any validation issue.
    """
    yaml_path = Path(path).resolve()
    if not yaml_path.exists():
        _fail(f"Workflow file not found: {yaml_path}")
    if yaml_path.suffix.lower() not in {".yml", ".yaml"}:
        _fail(f"Unsupported workflow file extension: {yaml_path.suffix}")

    try:
        raw = yaml.safe_load(yaml_path.read_text(encoding="utf-8")) or {}
    except yaml.YAMLError as e:
        _fail(f"YAML parse error in {yaml_path}: {e}")

    if not isinstance(raw, dict):
        _fail(f"Top-level YAML must be a mapping (file: {yaml_path})")

    name = raw.get("name")
    if not name or not isinstance(name, str):
        _fail("Missing required string field: name")
    if name not in SUPPORTED_WORKFLOWS:
        _fail(f"Invalid test: {name!r}. Supported: {sorted(SUPPORTED_WORKFLOWS)}")
    
    # naming conventions
    if any(c for c in name if c.isupper()) or " " in name:
        _fail("Workflow name should be lowercase kebab/underscore (no spaces or uppercase)")

    tests_rel = raw.get("tests")
    if not tests_rel or not isinstance(tests_rel, str):
        _fail("Missing required string field: tests")
    tests_path = (yaml_path.parent / tests_rel).resolve()
    if not tests_path.exists():
        _fail(f"Tests file not found: {tests_path}")
    if tests_path.suffix != ".jsonl":
        _fail(f"Tests file must be .jsonl: {tests_path}")

    kind = raw.get("kind")
    if not kind or not isinstance(kind, str):
        _fail("Missing required string field: kind")
    prompty_path: Path | None = None
    if kind == "prompt":
        prompty_rel = raw.get("prompty")
        if not prompty_rel or not isinstance(prompty_rel, str):
            _fail("kind=prompt requires field: prompty")
        prompty_path = (yaml_path.parent / prompty_rel).resolve()
        if not prompty_path.exists():
            _fail(f"Prompty file not found: {prompty_path}")
        if not prompty_path.suffix.startswith(".prompty"):
            _fail(f"Prompty path does not point to a prompty file")

    runs = raw.get("runs", 1)
    if not isinstance(runs, int) or runs < 1:
        _fail(f"runs must be positive integer (got: {runs!r})")


    cfg = WorkflowConfig(
        name=name,
        kind=kind,
        tests_path=tests_path,
        prompty_path=prompty_path,
        source_file=yaml_path,
    )
    return cfg


def load_workflow_directory(dir_path: str | os.PathLike) -> List[WorkflowConfig]:
    base = Path(dir_path).resolve()
    if not base.exists() or not base.is_dir():
        _fail(f"Workflow directory not found: {base}")
    yaml_files = sorted([p for p in base.iterdir() if p.suffix in (".yaml", ".yml")])
    if not yaml_files:
        _fail(f"No workflow yaml files found in: {base}")

    configs: List[WorkflowConfig] = []
    seen_names: set[str] = set()
    for yf in yaml_files:
        cfg = load_workflow_config(yf)
        if cfg.name in seen_names:
            _fail(f"Duplicate workflow name detected: {cfg.name}")
        seen_names.add(cfg.name)
        configs.append(cfg)
    return configs


__all__ = [
    "WorkflowConfig",
    "WorkflowConfigError",
    "load_workflow_config",
    "load_workflow_directory",
]
