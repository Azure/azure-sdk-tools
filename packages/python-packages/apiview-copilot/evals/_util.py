import json
import yaml
from pathlib import Path
from typing import Any

def ensure_json_obj(val):
    """Helper to ensure input is a dict (parsed JSON)."""
    if isinstance(val, str):
        return json.loads(val)
    return val

def load_cache_lookup(testcase_ids: list[str], test_file_paths: list[Path]) -> dict[str, dict]:
    """Load cache for multiple testcases and return testcase_id -> result_row dictionary.
    
    Args:
        testcase_ids: List of testcase identifiers to load
        test_file_paths: Corresponding list of test file paths
        
    Returns:
        Dictionary mapping testcase_id to Azure AI result row
    """
    cache_lookup = {}
    
    for testcase_id, test_file_path in zip(testcase_ids, test_file_paths):
        cache_file = get_cache_file_path(testcase_id, test_file_path)
        
        if cache_file.exists():
            try:
                with cache_file.open("r", encoding="utf-8") as f:
                    cached_data = json.load(f)
                    if "row" in cached_data:
                        cache_lookup[testcase_id] = cached_data["row"]
            except (json.JSONDecodeError, IOError):
                # Skip corrupted cache files
                continue
    
    return cache_lookup

def append_results_to_cache(test_file_paths: list[Path], azure_results: list[dict]) -> None:
    """Save fresh evaluation results to individual cache files.
    
    Args:
        test_file_paths: List of test file paths corresponding to the results
        azure_results: List of Azure AI evaluation results to cache
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
            testcase_id = extract_testcase_id(row)
            if testcase_id and testcase_id in testcase_to_file:
                test_file_path = testcase_to_file[testcase_id]
                save_result_to_cache(testcase_id, test_file_path, row)


def extract_testcase_id(row: dict) -> str | None:
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


def get_cache_file_path(
        testcase_id: str, test_file_path: Path | None = None, cache_base_dir: Path | None = None
    ) -> Path:
    """Get cache file path for a specific testcase, mirroring test structure.
    
    Args:
        testcase_id: The test case identifier
        test_file_path: Path to the original test file (to determine structure)
        cache_base_dir: Base directory for cache files (defaults to evals/tests/cache)
        
    Returns:
        Path to the individual JSON cache file for this testcase
    """
    if cache_base_dir is None:
        cache_base_dir = Path(__file__).parent / "tests" / "cache"
    
    # If we have test file path, mirror its structure
    if test_file_path:
        tests_root = Path(__file__).parent / "tests"
        try:
            # Get relative path from tests root to test file's directory
            relative_path = test_file_path.parent.relative_to(tests_root)
            cache_dir = cache_base_dir / relative_path
        except ValueError:
            # Fallback if test_file_path is not under tests root
            cache_dir = cache_base_dir / "misc"
    else:
        cache_dir = cache_base_dir / "legacy"
    
    cache_dir.mkdir(parents=True, exist_ok=True)
    return cache_dir / f"{testcase_id}.json"

def save_result_to_cache(testcase_id: str, test_file_path: Path, azure_result_row: dict) -> None:
    """Save a single test result to its individual cache file.
    
    Args:
        testcase_id: The test case identifier
        test_file_path: Path to the original test file
        azure_result_row: Single Azure AI evaluation result row to cache
    """
    cache_file = get_cache_file_path(testcase_id, test_file_path)
    
    cache_data = {
        "testcase": testcase_id,
        "row": azure_result_row
    }
    
    try:
        with cache_file.open("w", encoding="utf-8") as f:
            json.dump(cache_data, f, indent=2, ensure_ascii=False)
    except IOError:
        # Continue without caching if write fails
        pass

def construct_fake_azure_result(cached_rows: list[dict]) -> dict:
    """Construct a fake Azure AI evaluation result from cached rows."""
    # Extract the actual Azure AI result rows from cache
    result_rows = []
    for cached_row in cached_rows:
        if "row" in cached_row:
            result_rows.append(cached_row["row"])
    
    # Create a minimal Azure AI evaluation result structure
    fake_result = {
        "rows": result_rows,
        "metrics": {},  # Azure AI framework will populate this
        "studio_url": None  # Not needed for cached results
    }
    
    return fake_result