import json
from pathlib import Path
from typing import Any

def ensure_json_obj(val):
    """Helper to ensure input is a dict (parsed JSON)."""
    if isinstance(val, str):
        return json.loads(val)
    return val

def load_cache_lookup(cache_file: Path) -> dict[str, dict]:
    """Load cache file and return testcase_id -> result_row dictionary for O(1) lookup.
    
    Args:
        cache_file: Path to the cache JSONL file
        
    Returns:
        Dictionary mapping testcase_id to Azure AI result row
    """
    if not cache_file.exists():
        return {}
    
    cache_lookup = {}
    try:
        with cache_file.open("r", encoding="utf-8") as f:
            for line in f:
                line = line.strip()
                if line:
                    cached_item = json.loads(line)
                    if "testcase" in cached_item and "row" in cached_item:
                        cache_lookup[cached_item["testcase"]] = cached_item["row"]
    except (json.JSONDecodeError, IOError):
        return {}  # Corrupted cache, return empty dict
    
    return cache_lookup


def append_results_to_cache(cache_file: Path, azure_results: list[dict]) -> None:
    """Append fresh evaluation results to cache file.
    
    Args:
        cache_file: Path to the cache JSONL file
        azure_results: List of Azure AI evaluation results to cache
    """
    # Load existing cache to avoid duplicates
    existing_cache = load_cache_lookup(cache_file)
    
    # Prepare new entries
    new_entries = []
    for azure_result in azure_results:
        for row in azure_result.get("rows", []):
            testcase_id = extract_testcase_id(row)
            if testcase_id and testcase_id not in existing_cache:
                new_entries.append({
                    "testcase": testcase_id,
                    "row": row
                })
    
    # Append new entries to file
    if new_entries:
        try:
            # Ensure parent directory exists
            cache_file.parent.mkdir(parents=True, exist_ok=True)
            
            with cache_file.open("a", encoding="utf-8") as f:  # Append mode
                for entry in new_entries:
                    f.write(json.dumps(entry, separators=(",", ":"), ensure_ascii=False) + "\n")
        except IOError:
            pass  # Continue without caching if write fails


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


def get_cache_file_path(workflow_name: str, cache_base_dir: Path | None = None) -> Path:
    """Get cache file path for a workflow.
    
    Args:
        workflow_name: Name of the evaluation workflow
        cache_base_dir: Base directory for cache files (defaults to evals/tests/cache)
        
    Returns:
        Path to the cache file for this workflow
    """
    if cache_base_dir is None:
        # Default to evals/tests/cache relative to this file
        cache_base_dir = Path(__file__).parent / "tests" / "cache"
    
    cache_base_dir.mkdir(parents=True, exist_ok=True)
    return cache_base_dir / f"{workflow_name}.jsonl"

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