# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""
Tests for discovery functionality in APIView Copilot evals.
"""

import tempfile
from pathlib import Path
from unittest.mock import patch

import pytest

from evals._discovery import (
    discover_targets,
    _discover_from_path,
    _discover_single_test,
    _find_workflow_config
)


class MockWorkflowConfig:
    def __init__(self, name="test-workflow", kind="prompt"):
        self.name = name
        self.kind = kind


def test_discover_targets_empty_paths():
    with tempfile.TemporaryDirectory() as temp_dir:
        tests_root = Path(temp_dir)
        
        with pytest.raises(ValueError, match="No workflows found"):
            discover_targets(None, tests_root)


def test_discover_targets_nonexistent_tests_root():
    nonexistent = Path("/nonexistent/path")
    
    with pytest.raises(ValueError, match="Tests directory not found"):
        discover_targets(None, nonexistent)


@patch('evals._discovery.load_workflow_config')
def test_discover_single_test_file(mock_load_config):
    mock_config = MockWorkflowConfig("test-workflow", "prompt")
    mock_load_config.return_value = mock_config
    
    with tempfile.TemporaryDirectory() as temp_dir:
        temp_path = Path(temp_dir)
        
        # Create test structure
        config_file = temp_path / "test-config.yaml"
        config_file.write_text("name: test-workflow\nkind: prompt")
        
        test_file = temp_path / "test1.json"
        test_file.write_text('{"test": "data"}')
        
        targets = _discover_single_test(test_file)
        
        assert len(targets) == 1
        target = targets[0]
        assert target.type == "single_test"
        assert target.workflow_name == "test-workflow"
        assert target.test_files == (test_file,)
        

@patch('evals._discovery.load_workflow_config')
def test_discover_from_path_workflow_directory(mock_load_config):
    mock_config = MockWorkflowConfig("workflow-test", "apiview")
    mock_load_config.return_value = mock_config
    
    with tempfile.TemporaryDirectory() as temp_dir:
        workflow_dir = Path(temp_dir) / "workflow1"
        workflow_dir.mkdir()
        
        config_file = workflow_dir / "test-config.yaml"
        config_file.write_text("name: workflow-test\nkind: apiview")
        
        test_file1 = workflow_dir / "test1.json"
        test_file1.write_text('{"test": 1}')
        test_file2 = workflow_dir / "test2.yaml"
        test_file2.write_text('test: 2')
        
        targets = _discover_from_path(workflow_dir)
        
        assert len(targets) == 1
        target = targets[0]
        assert target.type == "workflow"
        assert target.workflow_name == "workflow-test"
        assert len(target.test_files) == 2
        assert all(f in target.test_files for f in [test_file1, test_file2])


def test_find_workflow_config_found_in_parent():
    with tempfile.TemporaryDirectory() as temp_dir:
        temp_path = Path(temp_dir)
        
        config_file = temp_path / "test-config.yaml"
        config_file.write_text("name: test")
        
        subdir = temp_path / "subdir"
        subdir.mkdir()
        
        result = _find_workflow_config(subdir)
        assert result == config_file


@patch('evals._discovery.load_workflow_config')
def test_discover_targets_with_grouping(mock_load_config):
    mock_config = MockWorkflowConfig("shared-config", "prompt")
    mock_load_config.return_value = mock_config
    
    with tempfile.TemporaryDirectory() as temp_dir:
        temp_path = Path(temp_dir)
        
        # Create shared config
        config_file = temp_path / "test-config.yaml"
        config_file.write_text("name: shared-config\nkind: prompt")
        
        # Create multiple test files in same directory
        test1 = temp_path / "test1.json"
        test1.write_text('{"test": 1}')
        test2 = temp_path / "test2.yaml"
        test2.write_text('test: 2')
        
        result = discover_targets([str(test1), str(test2)])
        
        # Should be grouped into single workflow target
        assert len(result.targets) == 1
        target = result.targets[0]
        assert target.type == "workflow"
        assert target.workflow_name == "shared-config"
        assert len(target.test_files) == 2


@patch('evals._discovery.load_workflow_config')
def test_mixed_grouping_single_and_multiple(mock_load_config):
    """Test mixing single tests that group vs single tests that don't."""
    mock_config1 = MockWorkflowConfig("workflow-1", "prompt")
    mock_config2 = MockWorkflowConfig("workflow-2", "apiview")
    
    def config_side_effect(path):
        if "dir1" in str(path):
            return mock_config1
        return mock_config2
    
    mock_load_config.side_effect = config_side_effect
    
    with tempfile.TemporaryDirectory() as temp_dir:
        temp_path = Path(temp_dir)
        
        # Directory 1: multiple tests, same config (should group)
        dir1 = temp_path / "dir1"
        dir1.mkdir()
        config1 = dir1 / "test-config.yaml"
        config1.write_text("name: workflow-1\nkind: prompt")
        test1 = dir1 / "test1.json"
        test1.write_text('{}')
        test2 = dir1 / "test2.json"
        test2.write_text('{}')
        
        # Directory 2: single test (should remain single)
        dir2 = temp_path / "dir2"
        dir2.mkdir()
        config2 = dir2 / "test-config.yaml"
        config2.write_text("name: workflow-2\nkind: apiview")
        test3 = dir2 / "test3.json"
        test3.write_text('{}')
        
        result = discover_targets([str(test1), str(test2), str(test3)])
        
        assert len(result.targets) == 2
        
        # Find grouped and single targets
        grouped = next(t for t in result.targets if t.type == "workflow")
        single = next(t for t in result.targets if t.type == "single_test")
        
        assert grouped.workflow_name == "workflow-1"
        assert len(grouped.test_files) == 2
        assert single.workflow_name == "workflow-2"
        assert len(single.test_files) == 1


@patch('evals._discovery.load_workflow_config')
def test_grouping_across_subdirectories(mock_load_config):
    """Test grouping when tests are in subdirectories but share parent config."""
    mock_config = MockWorkflowConfig("parent-workflow", "prompt")
    mock_load_config.return_value = mock_config
    
    with tempfile.TemporaryDirectory() as temp_dir:
        temp_path = Path(temp_dir)
        
        # Parent config
        config_file = temp_path / "test-config.yaml"
        config_file.write_text("name: parent-workflow\nkind: prompt")
        
        # Tests in subdirectories
        sub1 = temp_path / "sub1"
        sub1.mkdir()
        test_a = sub1 / "test_a.json"
        test_a.write_text('{}')
        
        sub2 = temp_path / "sub2"
        sub2.mkdir()
        test_b = sub2 / "test_b.json"
        test_b.write_text('{}')
        
        result = discover_targets([str(test_a), str(test_b)])
        
        assert len(result.targets) == 1
        target = result.targets[0]
        assert target.type == "workflow"
        assert target.workflow_name == "parent-workflow"
        assert len(target.test_files) == 2


@patch('evals._discovery.load_workflow_config')
def test_no_grouping_different_configs(mock_load_config):
    """Test that tests with different configs don't group together."""
    config1 = MockWorkflowConfig("workflow-1", "prompt")
    config2 = MockWorkflowConfig("workflow-2", "apiview")
    
    def config_side_effect(path):
        if "dir1" in str(path):
            return config1
        return config2
    
    mock_load_config.side_effect = config_side_effect
    
    with tempfile.TemporaryDirectory() as temp_dir:
        temp_path = Path(temp_dir)
        
        # Two separate workflows
        for i, (config_content, workflow_name) in enumerate([
            ("name: workflow-1\nkind: prompt", "workflow-1"),
            ("name: workflow-2\nkind: apiview", "workflow-2")
        ], 1):
            dir_path = temp_path / f"dir{i}"
            dir_path.mkdir()
            config_file = dir_path / "test-config.yaml"
            config_file.write_text(config_content)
            test_file = dir_path / f"test{i}.json"
            test_file.write_text('{}')
        
        test1 = temp_path / "dir1" / "test1.json"
        test2 = temp_path / "dir2" / "test2.json"
        
        result = discover_targets([str(test1), str(test2)])
        
        assert len(result.targets) == 2
        assert all(t.type == "single_test" for t in result.targets)
        workflow_names = {t.workflow_name for t in result.targets}
        assert workflow_names == {"workflow-1", "workflow-2"}


@patch('evals._discovery.load_workflow_config')
def test_empty_directory_handling(mock_load_config):
    """Test behavior with directories that have configs but no test files."""
    mock_config = MockWorkflowConfig("empty-workflow", "prompt")
    mock_load_config.return_value = mock_config
    
    with tempfile.TemporaryDirectory() as temp_dir:
        workflow_dir = Path(temp_dir) / "empty_workflow"
        workflow_dir.mkdir()
        
        config_file = workflow_dir / "test-config.yaml"
        config_file.write_text("name: empty-workflow\nkind: prompt")
        
        # No test files, only config
        with pytest.raises(ValueError, match="No test files found in workflow"):
            _discover_from_path(workflow_dir)


@patch('evals._discovery.load_workflow_config')
def test_merge_same_workflow_name_different_kinds(mock_load_config):
    """Test that configs with same name but different kinds don't group together."""
    config1 = MockWorkflowConfig("test", "prompt")
    config2 = MockWorkflowConfig("test", "apiview")  # Same name, different kind
    
    def config_side_effect(path):
        if "dir1" in str(path):
            return config1
        return config2
    
    mock_load_config.side_effect = config_side_effect
    
    with tempfile.TemporaryDirectory() as temp_dir:
        temp_path = Path(temp_dir)
        
        # Directory 1: prompt workflow named "test"
        dir1 = temp_path / "dir1"
        dir1.mkdir()
        config1_file = dir1 / "test-config.yaml"
        config1_file.write_text("name: test\nkind: prompt")
        test1 = dir1 / "test1.json"
        test1.write_text('{}')
        
        # Directory 2: apiview workflow also named "test"
        dir2 = temp_path / "dir2"
        dir2.mkdir()
        config2_file = dir2 / "test-config.yaml"
        config2_file.write_text("name: test\nkind: apiview")
        test2 = dir2 / "test2.json"
        test2.write_text('{}')
        
        result = discover_targets([str(test1), str(test2)])
        
        # Should NOT group - different kinds mean different workflows
        assert len(result.targets) == 2
        assert all(t.type == "single_test" for t in result.targets)
        
        # Verify both have same name but are treated separately
        workflow_configs = {(t.workflow_name, getattr(t.config, 'kind', None)) 
                           for t in result.targets}
        assert workflow_configs == {("test", "prompt"), ("test", "apiview")}


@patch('evals._discovery.load_workflow_config')
def test_config_hierarchy_ambiguity(mock_load_config):
    """Test config resolution when test is equidistant from multiple configs."""
    root_config = MockWorkflowConfig("root-workflow", "prompt")
    sub_config = MockWorkflowConfig("sub-workflow", "apiview")
    
    def config_side_effect(path):
        # The _find_workflow_config should prefer closer/more specific config
        if "sub" in str(path):
            return sub_config
        return root_config
    
    mock_load_config.side_effect = config_side_effect
    
    with tempfile.TemporaryDirectory() as temp_dir:
        temp_path = Path(temp_dir)
        
        # Root level config
        root_config_file = temp_path / "test-config.yaml"
        root_config_file.write_text("name: root-workflow\nkind: prompt")
        
        # Subdirectory with its own config
        sub_dir = temp_path / "sub"
        sub_dir.mkdir()
        sub_config_file = sub_dir / "test-config.yaml"
        sub_config_file.write_text("name: sub-workflow\nkind: apiview")
        
        # Test file in subdirectory (closer to sub config)
        test_file = sub_dir / "test.json"
        test_file.write_text('{}')
        
        targets = _discover_single_test(test_file)
        
        # Should use the closer/more specific config
        assert len(targets) == 1
        target = targets[0]
        assert target.workflow_name == "sub-workflow"
        

@patch('evals._discovery.load_workflow_config')
def test_grouping_with_duplicate_test_names(mock_load_config):
    """Test that duplicate test filenames across workflows don't collide."""
    workflow1_config = MockWorkflowConfig("workflow-1", "prompt")
    workflow2_config = MockWorkflowConfig("workflow-2", "apiview")
    
    def config_side_effect(path):
        if "workflow1" in str(path):
            return workflow1_config
        return workflow2_config
    
    mock_load_config.side_effect = config_side_effect
    
    with tempfile.TemporaryDirectory() as temp_dir:
        temp_path = Path(temp_dir)
        
        # Workflow 1 directory with test.json
        workflow1_dir = temp_path / "workflow1"
        workflow1_dir.mkdir()
        config1 = workflow1_dir / "test-config.yaml"
        config1.write_text("name: workflow-1\nkind: prompt")
        test1 = workflow1_dir / "test.json"  # Same filename
        test1.write_text('{"workflow": 1}')
        
        # Workflow 2 directory with test.json
        workflow2_dir = temp_path / "workflow2"
        workflow2_dir.mkdir()
        config2 = workflow2_dir / "test-config.yaml"
        config2.write_text("name: workflow-2\nkind: apiview")
        test2 = workflow2_dir / "test.json"  # Same filename, different path
        test2.write_text('{"workflow": 2}')
        
        result = discover_targets([str(test1), str(test2)])
        
        # Should be two separate targets, no collision
        assert len(result.targets) == 2
        assert all(t.type == "single_test" for t in result.targets)
        
        workflow_names = {t.workflow_name for t in result.targets}
        assert workflow_names == {"workflow-1", "workflow-2"}
        
        # Verify each target has the correct test file path
        for target in result.targets:
            assert len(target.test_files) == 1
            test_file = target.test_files[0]
            assert test_file.name == "test.json"
            if target.workflow_name == "workflow-1":
                assert "workflow1" in str(test_file)
            else:
                assert "workflow2" in str(test_file)


@patch('evals._discovery.load_workflow_config')
def test_mixed_extensions_same_workflow(mock_load_config):
    """Test that different file extensions are discovered consistently in same workflow."""
    mock_config = MockWorkflowConfig("multi-format", "prompt")
    mock_load_config.return_value = mock_config
    
    with tempfile.TemporaryDirectory() as temp_dir:
        temp_path = Path(temp_dir)
        
        # Single workflow config
        config_file = temp_path / "test-config.yaml"
        config_file.write_text("name: multi-format\nkind: prompt")
        
        # Create test files with different extensions
        json_test = temp_path / "test.json"
        json_test.write_text('{"format": "json"}')
        
        yaml_test = temp_path / "test.yaml"
        yaml_test.write_text('format: yaml')
        
        yml_test = temp_path / "test.yml"
        yml_test.write_text('format: yml')
        
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
        assert extensions == {'.json', '.yaml', '.yml'}

        # Verify order is consistent (should be deterministic)
        file_names = [f.name for f in sorted(target.test_files)]
        expected_names = ['test.json', 'test.yaml', 'test.yml']
        assert file_names == expected_names