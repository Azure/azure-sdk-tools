# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""
Tests for discovery functionality in APIView Copilot evals.
"""

from pathlib import Path

# Import to register evaluators
import evals._custom  # pylint: disable=unused-import
import pytest
from evals._discovery import (
    _discover_from_path,
    _discover_single_test,
    _find_workflow_config,
    discover_targets,
)

# Path to test fixtures
FIXTURES_DIR = Path(__file__).parent / "fixtures"


class MockWorkflowConfig:
    def __init__(self, name="test-workflow", kind="prompt"):
        self.name = name
        self.kind = kind


def test_discover_targets_empty_paths():
    empty_dir = FIXTURES_DIR / "empty-workflow"

    with pytest.raises(ValueError, match="No test files found in workflow"):
        discover_targets(None, empty_dir)


def test_discover_targets_nonexistent_tests_root():
    nonexistent = Path("/nonexistent/path")

    with pytest.raises(ValueError, match="Tests directory not found"):
        discover_targets(None, nonexistent)


def test_discover_single_test_file():
    """Test discovering a single test file with its config."""
    test_file = FIXTURES_DIR / "test-workflow" / "test1.json"

    targets = _discover_single_test(test_file)

    assert len(targets) == 1
    target = targets[0]
    assert target.type == "single_test"
    assert target.workflow_name == "test-workflow"
    assert target.test_files == (test_file,)


def test_discover_from_path_workflow_directory():
    """Test discovering all tests in a workflow directory."""
    workflow_dir = FIXTURES_DIR / "workflow-test"

    targets = _discover_from_path(workflow_dir)

    assert len(targets) == 1
    target = targets[0]
    assert target.type == "workflow"
    assert target.workflow_name == "workflow-test"
    assert len(target.test_files) == 2
    test_files = {f.name for f in target.test_files}
    assert test_files == {"test1.json", "test2.yaml"}


def test_find_workflow_config_found_in_parent():
    """Test finding workflow config in parent directory."""
    subdir = FIXTURES_DIR / "parent-config-test" / "subdir"
    expected_config = FIXTURES_DIR / "parent-config-test" / "test-config.yaml"

    result = _find_workflow_config(subdir)
    assert result == expected_config


def test_discover_targets_with_grouping():
    """Test that multiple tests with same config get grouped."""
    test1 = FIXTURES_DIR / "shared-config" / "test1.json"
    test2 = FIXTURES_DIR / "shared-config" / "test2.yaml"

    result = discover_targets([str(test1), str(test2)])

    # Should be grouped into single workflow target
    assert len(result.targets) == 1
    target = result.targets[0]
    assert target.type == "workflow"
    assert target.workflow_name == "shared-config"
    assert len(target.test_files) == 2


def test_mixed_grouping_single_and_multiple():
    """Test mixing single tests that group vs single tests that don't."""
    test1 = FIXTURES_DIR / "workflow-1" / "test1.json"
    test2 = FIXTURES_DIR / "workflow-1" / "test2.json"
    test3 = FIXTURES_DIR / "workflow-2" / "test3.json"

    result = discover_targets([str(test1), str(test2), str(test3)])

    assert len(result.targets) == 2

    # Find grouped and single targets
    grouped = next(t for t in result.targets if t.type == "workflow")
    single = next(t for t in result.targets if t.type == "single_test")

    assert grouped.workflow_name == "workflow-1"
    assert len(grouped.test_files) == 2
    assert single.workflow_name == "workflow-2"
    assert len(single.test_files) == 1


def test_grouping_across_subdirectories():
    """Test grouping when tests are in subdirectories but share parent config."""
    test_a = FIXTURES_DIR / "parent-workflow" / "sub1" / "test_a.json"
    test_b = FIXTURES_DIR / "parent-workflow" / "sub2" / "test_b.json"

    result = discover_targets([str(test_a), str(test_b)])

    assert len(result.targets) == 1
    target = result.targets[0]
    assert target.type == "workflow"
    assert target.workflow_name == "parent-workflow"
    assert len(target.test_files) == 2


def test_no_grouping_different_configs():
    """Test that tests with different configs don't group together."""
    test1 = FIXTURES_DIR / "workflow-1" / "test1.json"
    test2 = FIXTURES_DIR / "workflow-2" / "test3.json"

    result = discover_targets([str(test1), str(test2)])

    assert len(result.targets) == 2
    assert all(t.type == "single_test" for t in result.targets)
    workflow_names = {t.workflow_name for t in result.targets}
    assert workflow_names == {"workflow-1", "workflow-2"}


def test_empty_directory_handling():
    """Test behavior with directories that have configs but no test files."""
    workflow_dir = FIXTURES_DIR / "empty-workflow"

    # No test files, only config
    with pytest.raises(ValueError, match="No test files found in workflow"):
        _discover_from_path(workflow_dir)


def test_merge_same_workflow_name_different_kinds():
    """Test that configs with same name but different kinds don't group together."""
    test1 = FIXTURES_DIR / "same-name-different-kinds" / "test-prompt-kind" / "test1.json"
    test2 = FIXTURES_DIR / "same-name-different-kinds" / "test-apiview-kind" / "test2.json"

    result = discover_targets([str(test1), str(test2)])

    # Should NOT group - different kinds mean different workflows
    assert len(result.targets) == 2
    assert all(t.type == "single_test" for t in result.targets)

    # Verify both have same name but are treated separately
    workflow_configs = {(t.workflow_name, getattr(t.config, "kind", None)) for t in result.targets}
    assert workflow_configs == {("test-prompt-kind", "prompt"), ("test-apiview-kind", "summarize_prompt")}


def test_config_hierarchy_ambiguity():
    """Test config resolution when test is equidistant from multiple configs."""
    test_file = FIXTURES_DIR / "config-hierarchy" / "sub" / "test.json"

    targets = _discover_single_test(test_file)

    # Should use the closer/more specific config
    assert len(targets) == 1
    target = targets[0]
    assert target.workflow_name == "sub"


def test_grouping_with_duplicate_test_names():
    """Test that duplicate test filenames across workflows don't collide."""
    test1 = FIXTURES_DIR / "duplicate-names" / "workflow1" / "test.json"
    test2 = FIXTURES_DIR / "duplicate-names" / "workflow2" / "test.json"

    result = discover_targets([str(test1), str(test2)])

    # Should be two separate targets, no collision
    assert len(result.targets) == 2
    assert all(t.type == "single_test" for t in result.targets)

    workflow_names = {t.workflow_name for t in result.targets}
    assert workflow_names == {"workflow1", "workflow2"}

    # Verify each target has the correct test file path
    for target in result.targets:
        assert len(target.test_files) == 1
        test_file = target.test_files[0]
        assert test_file.name == "test.json"
        if target.workflow_name == "workflow1":
            assert "workflow1" in str(test_file)
        else:
            assert "workflow2" in str(test_file)


def test_mixed_extensions_same_workflow():
    """Test that different file extensions are discovered consistently in same workflow."""
    json_test = FIXTURES_DIR / "multi-format" / "test.json"
    yaml_test = FIXTURES_DIR / "multi-format" / "test.yaml"
    yml_test = FIXTURES_DIR / "multi-format" / "test.yml"

    all_files = [str(json_test), str(yaml_test), str(yml_test)]
    result = discover_targets(all_files)

    # Should group all files into single workflow regardless of extension
    assert len(result.targets) == 1
    target = result.targets[0]
    assert target.type == "workflow"
    assert target.workflow_name == "multi-format"
    assert len(target.test_files) == 3

    # Verify all extensions are included
    extensions = {f.suffix for f in target.test_files}
    assert extensions == {".json", ".yaml", ".yml"}

    # Verify order is consistent
    file_names = [f.name for f in sorted(target.test_files)]
    expected_names = ["test.json", "test.yaml", "test.yml"]
    assert file_names == expected_names
