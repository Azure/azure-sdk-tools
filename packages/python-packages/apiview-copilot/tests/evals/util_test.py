# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""
Regression test for evaluation runner bug fix in APIView Copilot evals.
"""

import json
from pathlib import Path
from unittest.mock import patch, MagicMock

import pytest

from evals._runner import EvaluationRunner


@pytest.fixture
def test_files():
    """Test file paths from fixtures."""
    fixtures_dir = Path(__file__).parent / "fixtures" / "cache-regression-test"
    return [
        fixtures_dir / "test1.json",
        fixtures_dir / "test2.json"
    ]


@pytest.fixture
def mock_target(test_files):
    """Mock evaluation target with fixture test files."""
    target = MagicMock()
    target.workflow_name = "cache_regression_test"
    target.test_files = test_files
    target.config = MagicMock()
    target.config.kind = "prompt"
    return target


@pytest.fixture
def sample_azure_rows():
    """Sample Azure AI evaluation result rows for testing."""
    return [
        {
            "inputs.testcase": "cache_test_1",
            "outputs.metrics.correct_action": True,
            "outputs.metrics.score": 0.85
        },
        {
            "inputs.testcase": "cache_test_2", 
            "outputs.metrics.correct_action": False,
            "outputs.metrics.score": 0.45
        }
    ]


@pytest.fixture
def mock_cache_lookup(sample_azure_rows):
    """Mock cache lookup function that returns sample Azure rows."""
    def _mock_load_cache_lookup(testcase_ids, test_file_paths):
        return {
            "cache_test_1": sample_azure_rows[0],
            "cache_test_2": sample_azure_rows[1]
        }
    return _mock_load_cache_lookup


@pytest.fixture
def mock_test_file_loader():
    """Mock test file loader that reads from fixtures."""
    def _mock_load_test_file(test_file):
        try:
            with test_file.open("r") as f:
                return json.load(f)
        except (FileNotFoundError, json.JSONDecodeError):
            if "test1.json" in str(test_file):
                return {"testcase": "cache_test_1", "input": "test input 1"}
            elif "test2.json" in str(test_file):
                return {"testcase": "cache_test_2", "input": "test input 2"}
            return {}
    return _mock_load_test_file


@pytest.fixture
def evaluation_runner():
    """Evaluation runner configured for testing."""
    runner = EvaluationRunner(use_cache=True)
    runner._ensure_context()
    return runner


def test_regression_execute_target_empty_rows_bug(mock_target, evaluation_runner, mock_test_file_loader, mock_cache_lookup):
    """
    Regression test: Ensure _execute_target never returns success=True with empty rows.
    
    This prevents the bug where _execute_target would return an EvaluationResult
    with success=True but combined_result["rows"] being an empty array, causing
    false positive evaluations where all tests appeared to pass.
    """
    with patch.object(evaluation_runner._context, '_load_test_file', side_effect=mock_test_file_loader), \
         patch('evals._runner.load_cache_lookup', side_effect=mock_cache_lookup):
        
        result = evaluation_runner._execute_target(mock_target)
        
        assert result.success is True
        assert len(result.raw_results) > 0
        
        raw_result = result.raw_results[0]
        combined_result = raw_result[f"{mock_target.workflow_name}.jsonl"]
        
        assert "rows" in combined_result
        assert isinstance(combined_result["rows"], list)
        assert len(combined_result["rows"]) > 0, \
            "Empty rows with success=True causes false positive evaluations"
        
        rows = combined_result["rows"]
        assert len(rows) == 2
        
        for row in rows:
            assert "inputs.testcase" in row
            assert "outputs.metrics.correct_action" in row
            assert "outputs.metrics.score" in row
        
        testcase_ids = [row["inputs.testcase"] for row in rows]
        assert "cache_test_1" in testcase_ids
        assert "cache_test_2" in testcase_ids
        
        results_by_testcase = {row["inputs.testcase"]: row["outputs.metrics.correct_action"] for row in rows}
        assert results_by_testcase["cache_test_1"] is True
        assert results_by_testcase["cache_test_2"] is False