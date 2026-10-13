import json
import os
from pathlib import Path
import subprocess
import sys


def main():
    raw = os.environ.get("QA_RESEARCH_DATASET")
    if not raw:
        raise ValueError("QA_RESEARCH_DATASET is required for the research holdout stage")
    rows = json.loads(raw)
    if not isinstance(rows, list) or not rows:
        raise ValueError("Research dataset must be a nonempty case array")
    folder = Path(__file__).resolve().parents[1] / "cache" / "research-inputs"
    folder.mkdir(parents=True, exist_ok=True)
    dataset = folder / "apispec.jsonl"
    with dataset.open("x", encoding="utf-8") as output:
        for row in rows:
            output.write(json.dumps(row) + "\n")
    subprocess.run([
        sys.executable, "-m", "dataset.validate", str(dataset), "--require-reviewed",
    ], check=True)
    subprocess.run([
        sys.executable, "evals_run.py", "--dataset", str(dataset),
        "--run_context", "research-holdout", "--max_concurrency", "2",
        "--baseline_check", "False", "--cache_result", "full",
    ], check=True)


if __name__ == "__main__":
    main()
