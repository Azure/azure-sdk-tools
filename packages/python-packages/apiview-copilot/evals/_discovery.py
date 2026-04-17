# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

import dataclasses
import os
import sys
from collections import defaultdict
from pathlib import Path
from typing import Literal

sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))

from evals._config_loader import WorkflowConfig, load_workflow_config


@dataclasses.dataclass(frozen=True)
class EvaluationTarget:
    """Represents a single evaluation unit - either a file or workflow."""

    path: Path
    type: Literal["single_test", "workflow", "workflow_directory"]
    workflow_name: str
    test_files: tuple[Path, ...]
    config: WorkflowConfig


@dataclasses.dataclass(frozen=True)
class DiscoveryResult:
    """Result of the discovery process."""

    targets: tuple[EvaluationTarget, ...]
    total_test_files: int
    workflow_types: set[str]

    @property
    def summary(self) -> str:
        """Human-readable summary of discovered targets."""
        return (
            f"Discovered {len(self.targets)} targets "
            f"({self.total_test_files} test files, "
            f"types: {', '.join(sorted(self.workflow_types))})"
        )


def discover_targets(test_paths: list[str] | None, tests_root: Path | None = None) -> DiscoveryResult:
    """Discover evaluation targets from the given paths.

    Args:
        test_paths: List of paths to discover from. If None, discovers all workflows.
        tests_root: Root directory for tests. If None, uses ./tests relative to this file.

    Returns:
        DiscoveryResult containing all discovered targets.

    Raises:
        ValueError: If paths are invalid or no targets found.
    """
    if tests_root is None:
        tests_root = Path(__file__).parent / "tests"

    if not test_paths:
        return _discover_all_workflows(tests_root)

    targets = []
    for path_str in test_paths:
        path = Path(path_str).resolve()
        targets.extend(_discover_from_path(path))

    # Group single tests with same config into workflows
    grouped_targets = _merge_targets_by_config(targets)

    if not grouped_targets:
        raise ValueError(f"No evaluation targets found in provided paths: {test_paths}")

    return _create_result(grouped_targets)


def _discover_all_workflows(tests_root: Path) -> DiscoveryResult:
    """Discover all workflows in the tests directory."""
    if not tests_root.exists():
        raise ValueError(f"Tests directory not found: {tests_root}")

    targets = []
    for item in tests_root.rglob("test-config.yaml"):
        workflow_dir = item.parent
        targets.extend(_discover_from_path(workflow_dir))

    # Group single tests with same config into workflows
    grouped_targets = _merge_targets_by_config(targets)

    if not grouped_targets:
        raise ValueError(f"No workflows found in: {tests_root}")

    return _create_result(grouped_targets)


def _discover_from_path(path: Path) -> list[EvaluationTarget]:
    """Discover targets from a single path."""
    if not path.exists():
        raise ValueError(f"Path does not exist: {path}")

    if path.is_file() and path.suffix in [".json", ".yaml", ".yml"]:
        return _discover_single_test(path)
    elif path.is_dir():
        return _discover_from_directory(path)
    else:
        raise ValueError(f"Invalid path (must be .json file or directory): {path}")


def _discover_single_test(test_file: Path) -> list[EvaluationTarget]:
    """Discover a single test file."""
    workflow_dir = test_file.parent

    # Find the workflow config (might be in parent directories)
    config_path = _find_workflow_config(workflow_dir)
    if not config_path:
        raise ValueError(f"No test-config.yaml found for test file: {test_file}")

    config = load_workflow_config(config_path.parent)

    return [
        EvaluationTarget(
            path=test_file, type="single_test", workflow_name=config.name, test_files=(test_file,), config=config
        )
    ]


def _discover_from_directory(directory: Path) -> list[EvaluationTarget]:
    """Discover targets from a directory."""
    config_file = directory / "test-config.yaml"

    if config_file.exists():
        # This is a single workflow directory
        return _discover_single_workflow(directory)
    else:
        # This might contain multiple workflows
        return _discover_workflow_directory(directory)


def _discover_single_workflow(workflow_dir: Path) -> list[EvaluationTarget]:
    """Discover a single workflow directory."""
    config = load_workflow_config(workflow_dir)
    all_files = list(workflow_dir.glob("*.json")) + list(workflow_dir.glob("*.yaml")) + list(workflow_dir.glob("*.yml"))

    test_files = tuple(sorted([f for f in all_files if f.name not in ["test-config.yaml", "test-config.yml"]]))

    if not test_files:
        raise ValueError(f"No test files found in workflow: {workflow_dir}")

    return [
        EvaluationTarget(
            path=workflow_dir, type="workflow", workflow_name=config.name, test_files=test_files, config=config
        )
    ]


def _discover_workflow_directory(directory: Path) -> list[EvaluationTarget]:
    """Discover multiple workflows in a directory."""
    targets = []

    for item in directory.iterdir():
        if item.is_dir() and (item / "test-config.yaml").exists():
            targets.extend(_discover_single_workflow(item))

    if not targets:
        raise ValueError(f"No workflows found in directory: {directory}")

    return targets


def _merge_targets_by_config(targets: list[EvaluationTarget]) -> list[EvaluationTarget]:
    """Merge single test targets that share the same workflow config."""
    config_groups = defaultdict(list)

    for target in targets:
        if target.type == "single_test":
            # Find the config path for this target to use as grouping key
            config_path = _find_workflow_config(target.path.parent)
            if config_path:
                config_groups[config_path].append(target)
            else:
                config_groups[target.path].append(target)
        else:
            # Workflow and workflow_directory targets don't get grouped
            config_groups[target.path].append(target)

    merged_targets = []
    for config_path, group_targets in config_groups.items():
        if len(group_targets) == 1:
            merged_targets.append(group_targets[0])
        elif all(t.type == "single_test" for t in group_targets):
            # Multiple single_test targets with same config - merge into workflow
            all_test_files = tuple(file for target in group_targets for file in target.test_files)

            merged_target = EvaluationTarget(
                path=config_path.parent if isinstance(config_path, Path) else group_targets[0].path.parent,
                type="workflow",
                workflow_name=group_targets[0].workflow_name,
                test_files=all_test_files,
                config=group_targets[0].config,
            )
            merged_targets.append(merged_target)
        else:
            # Mixed types or multiple non-single_test - keep separate
            merged_targets.extend(group_targets)

    return merged_targets


def _find_workflow_config(start_dir: Path) -> Path | None:
    """Find test-config.yaml in the directory or its parents."""
    current = start_dir
    for _ in range(5):  # Limit search depth
        config_file = current / "test-config.yaml"
        if config_file.exists():
            return config_file
        current = current.parent
        if current == current.parent:  # Reached filesystem root
            break
    return None


def _create_result(targets: list[EvaluationTarget]) -> DiscoveryResult:
    """Create a DiscoveryResult from discovered targets."""
    total_files = sum(len(target.test_files) for target in targets)
    workflow_types = {target.config.kind for target in targets}

    return DiscoveryResult(targets=tuple(targets), total_test_files=total_files, workflow_types=workflow_types)


__all__ = [
    "EvaluationTarget",
    "DiscoveryResult",
    "discover_targets",
]
