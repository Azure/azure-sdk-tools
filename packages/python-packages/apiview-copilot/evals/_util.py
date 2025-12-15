import json
import yaml
from pathlib import Path

def ensure_json_obj(val):
    """Helper to ensure input is a dict (parsed JSON)."""
    if isinstance(val, str):
        return json.loads(val)
    return val

def load_recordings(testcase_ids: list[str], test_file_paths: list[Path]) -> dict[str, dict]:
    """Load recordings for multiple testcases and return testcase_id -> result_row dictionary.

    Args:
        testcase_ids: List of testcase identifiers to load
        test_file_paths: Corresponding list of test file paths
        
    Returns:
        Dictionary mapping testcase_id to Azure AI result row
    """
    recordings_lookup = {}
    
    for testcase_id, test_file_path in zip(testcase_ids, test_file_paths):
        recording_file = _get_recording_file_path(testcase_id, test_file_path)

        if recording_file.exists():
            try:
                with recording_file.open("r", encoding="utf-8") as f:
                    cached_data = json.load(f)
                    if "row" in cached_data:
                        recordings_lookup[testcase_id] = cached_data["row"]
            except (json.JSONDecodeError, IOError):
                # Skip corrupted cache files
                continue

    return recordings_lookup

def save_recordings(test_file_paths: list[Path], azure_results: list[dict]) -> None:
    """Save fresh evaluation results to individual recording files.
    
    Args:
        test_file_paths: List of test file paths corresponding to the results
        azure_results: List of Azure AI evaluation results to save
    """
    # Build mapping from testcase_id to test_file_path
    testcase_to_file = {}
    for test_file_path in test_file_paths:
        # Load test file to get testcase_id
        try:
            with test_file_path.open("r", encoding="utf-8") as f:
                if test_file_path.suffix == ".json":
                    test_data = json.load(f)
                elif test_file_path.suffix in [".yaml", ".yml"]:
                    test_data = yaml.safe_load(f)
                else:
                    continue
                    
                testcase_id = test_data.get("testcase")
                if testcase_id:
                    testcase_to_file[testcase_id] = test_file_path
        except (json.JSONDecodeError, IOError, yaml.YAMLError):
            continue
    
    # Save each result to its individual cache file
    for azure_result in azure_results:
        for row in azure_result.get("rows", []):
            testcase_id = _extract_testcase_id(row)
            if testcase_id and testcase_id in testcase_to_file:
                test_file_path = testcase_to_file[testcase_id]
                _save_result_to_file(testcase_id, test_file_path, row)


def _extract_testcase_id(row: dict) -> str | None:
    """Extract testcase identifier from Azure AI evaluation result row.
    
    Args:
        row: Azure AI evaluation result row
        
    Returns:
        Testcase ID string, or None if not found
    """
    if "inputs.testcase" in row:
        return row["inputs.testcase"]
    elif "testcase" in row:
        return row["testcase"]
    elif "inputs" in row and isinstance(row["inputs"], dict):
        return row["inputs"].get("testcase")
    return None


def _get_recording_file_path(
        testcase_id: str, test_file_path: Path | None = None, recording_base_dir: Path | None = None
    ) -> Path:
    """Get recording file path for a specific testcase, mirroring test structure.
    
    Args:
        testcase_id: The test case identifier
        test_file_path: Path to the original test file (to determine structure)
        recording_base_dir: Base directory for recording files (defaults to evals/recordings)
        
    Returns:
        Path to the individual JSON recording file for this testcase
    """
    if recording_base_dir is None:
        recording_base_dir = Path(__file__).parent / "recordings"

    # If we have test file path, mirror its structure
    if test_file_path:
        tests_root = Path(__file__).parent / "tests"
        try:
            # Get relative path from tests root to test file's directory
            relative_path = test_file_path.parent.relative_to(tests_root)
            recording_dir = recording_base_dir / relative_path
        except ValueError:
            # Fallback if test_file_path is not under tests root
            recording_dir = recording_base_dir / "misc"
    else:
        recording_dir = recording_base_dir / "legacy"

    recording_dir.mkdir(parents=True, exist_ok=True)
    return recording_dir / f"{testcase_id}.json"


def _save_result_to_file(testcase_id: str, test_file_path: Path, azure_result_row: dict) -> None:
    """Save a single test result to its individual recording file.
    
    Args:
        testcase_id: The test case identifier
        test_file_path: Path to the original test file
        azure_result_row: Single Azure AI evaluation result row to cache
    """
    recording_file = _get_recording_file_path(testcase_id, test_file_path)
    recording_data = {
        "testcase": testcase_id,
        "row": azure_result_row
    }
    
    try:
        with recording_file.open("w", encoding="utf-8") as f:
            json.dump(recording_data, f, indent=2, ensure_ascii=False)
    except IOError:
        # Continue without caching if write fails
        pass
    