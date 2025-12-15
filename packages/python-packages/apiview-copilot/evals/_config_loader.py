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
from typing import Dict, Type

import yaml
from evals._custom import (
    PromptyEvaluator,
    PromptySummaryEvaluator,
)

# Global evaluator registry
_EVALUATOR_REGISTRY: Dict[str, Type] = {}


@dataclasses.dataclass(slots=True)
class WorkflowConfig:
    name: str
    kind: str
    source_file: Path | None = None  # for diagnostics


class WorkflowConfigError(ValueError):
    pass


def register_evaluator(kind: str, evaluator_class: Type) -> None:
    """Register an evaluator class for a given kind.

    Args:
        kind: The workflow kind (e.g., 'apiview', 'prompt')
        evaluator_class: The evaluator class to register
    """
    _EVALUATOR_REGISTRY[kind] = evaluator_class


def _fail(msg: str) -> None:
    raise WorkflowConfigError(msg)


def get_evaluator_class(kind: str) -> Type:
    """Get an evaluator class by kind.

    Args:
        kind: The workflow kind

    Returns:
        The evaluator class

    Raises:
        WorkflowConfigError: If the kind is not registered
    """
    if kind not in _EVALUATOR_REGISTRY:
        available = sorted(_EVALUATOR_REGISTRY.keys())
        raise WorkflowConfigError(f"Unknown evaluator kind: {kind!r}. Available: {available}")
    return _EVALUATOR_REGISTRY[kind]


def get_supported_workflows() -> set[str]:
    """Get the set of supported workflow kinds."""
    return set(_EVALUATOR_REGISTRY.keys())


def load_workflow_config(path: str | os.PathLike) -> WorkflowConfig:
    """Load and validate a single workflow yaml file.

    Args:
        path: Path to a directory containing `test-config.yaml`.
              The workflow name will be inferred from the directory name.
    Returns:
        WorkflowConfig
    Raises:
        WorkflowConfigError for any validation issue.
    """
    input_path = Path(path).resolve()

    # require a directory (we expect a folder containing test-config.yaml)
    if not input_path.exists():
        _fail(
            f"Workflow path must be either a directory containing 'test-config.yaml' or a sibling of 'test-config.yaml': {input_path}"
        )

    base_dir = input_path

    # look for "test-config.yaml" or "test-config.yml" inside the directory, or as a sibling of the input path
    parent_dir = base_dir.parent
    candidates = [
        base_dir / "test-config.yaml",
        base_dir / "test-config.yml",
        parent_dir / "test-config.yaml",
        parent_dir / "test-config.yml",
    ]
    yaml_path = None
    for c in candidates:
        if c.exists() and c.is_file():
            yaml_path = c.resolve()
            break
    if not yaml_path:
        _fail(f"Workflow directory provided but no 'test-config.yaml' found in: {base_dir}")

    try:
        raw = yaml.safe_load(yaml_path.read_text(encoding="utf-8")) or {}
    except yaml.YAMLError as e:
        _fail(f"YAML parse error in {yaml_path}: {e}")

    if not isinstance(raw, dict):
        _fail(f"Top-level YAML must be a mapping (file: {yaml_path})")

    # Derive the workflow name from the directory basename (folder name).
    name = base_dir.name if yaml_path.parent == base_dir else yaml_path.parent.name
    if not name or not isinstance(name, str):
        _fail("Could not derive workflow name from the directory path")

    kind = raw.get("kind")
    if not kind or not isinstance(kind, str):
        _fail("Missing required string field: kind")

    supported_workflows = get_supported_workflows()
    if kind not in supported_workflows:
        _fail(f"Invalid kind: {kind!r}. Supported: {sorted(supported_workflows)}")

    runs = raw.get("runs", 1)
    if not isinstance(runs, int) or runs < 1:
        _fail(f"runs must be positive integer (got: {runs!r})")

    return WorkflowConfig(
        name=name,
        kind=kind,
        source_file=yaml_path,
    )


# Register evaluators at module load time to prevent circular imports
register_evaluator("prompt", PromptyEvaluator)
register_evaluator("summarize_prompt", PromptySummaryEvaluator)


__all__ = [
    "WorkflowConfig",
    "WorkflowConfigError",
    "register_evaluator",
    "get_evaluator_class",
    "get_supported_workflows",
    "load_workflow_config",
]
