"""Upload curated per-scenario datasets as Foundry versioned Dataset assets.

Third step of *dataset preparation*. Each curated file becomes one Foundry Dataset
asset named ``qa-bot-<target>-<scenario>`` with version ``<scenario>-YYYY-MM-DD``
(O3: one asset per scenario, named/versioned by scenario + time). The returned
asset id is recorded in ``evaluation_datasets/registry.json`` so evaluation runs can resolve
``--dataset <name:latest>`` to an asset id.

Usage:
    python -m dataset.upload --target basic
    python -m dataset.upload --target perf --scenario typespec
"""

from __future__ import annotations

import argparse
import json
import logging
import os
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from dotenv import load_dotenv

from .schema import validate_file

REGISTRY_NAME = "registry.json"


def asset_name(target: str, scenario: str) -> str:
    return f"qa-bot-{target}-{scenario}"


def asset_version(scenario: str) -> str:
    return f"{scenario}-{datetime.now(timezone.utc).strftime('%Y-%m-%d')}"


def _credential() -> Any:
    # Dataset preparation runs locally only and authenticates via ``az login``.
    from azure.identity import AzureCliCredential

    return AzureCliCredential()


def load_registry(path: Path) -> dict[str, Any]:
    if path.exists():
        with path.open("r", encoding="utf-8") as fh:
            return json.load(fh)
    return {}


def save_registry(path: Path, registry: dict[str, Any]) -> None:
    with path.open("w", encoding="utf-8") as fh:
        json.dump(registry, fh, indent=2, sort_keys=True)
        fh.write("\n")


def upload(target_dir: Path, target: str, scenario_filter: str | None, registry_path: Path) -> dict[str, str]:
    """Upload each curated per-scenario file as a versioned Dataset asset.

    Returns a mapping of ``asset_name -> asset_id`` for the uploaded assets.
    """
    from azure.ai.projects import AIProjectClient

    endpoint = os.environ["AZURE_AI_PROJECT_ENDPOINT"]
    files = sorted(target_dir.glob("*.jsonl"))
    if scenario_filter:
        files = [f for f in files if f.stem == scenario_filter]
    if not files:
        logging.info("No curated files to upload in %s.", target_dir)
        return {}

    registry = load_registry(registry_path)
    uploaded: dict[str, str] = {}

    credential = _credential()
    with AIProjectClient(endpoint=endpoint, credential=credential) as project_client:
        for f in files:
            scenario = f.stem
            # Fail fast if any row is not 'pass' / invalid before publishing.
            n = validate_file(f, require_reviewed=True)
            name = asset_name(target, scenario)
            version = asset_version(scenario)
            logging.info("Uploading %s (%d cases) as %s:%s", f.name, n, name, version)
            ds = project_client.datasets.upload_file(name=name, version=version, file_path=str(f))
            asset_id = getattr(ds, "id", "") or ""
            uploaded[name] = asset_id
            registry[name] = {
                "version": version,
                "id": asset_id,
                "target": target,
                "scenario": scenario,
                "cases": n,
                "uploaded_at": datetime.now(timezone.utc).isoformat(),
            }
            logging.info("  -> id=%s", asset_id)

    save_registry(registry_path, registry)
    logging.info("Registry updated: %s", registry_path)
    return uploaded


def main(argv: list[str] | None = None) -> int:
    logging.basicConfig(level=logging.INFO, stream=sys.stdout, format="%(asctime)s - %(levelname)s - %(message)s")
    parser = argparse.ArgumentParser(description="Upload curated datasets as Foundry versioned assets.")
    parser.add_argument("--target", choices=["basic", "perf"], required=True)
    parser.add_argument("--scenario", type=str, default=None, help="Only this scenario (file stem).")
    args = parser.parse_args(argv)

    load_dotenv()
    script_dir = Path(__file__).resolve().parent.parent
    target_dir = script_dir / "evaluation_datasets" / args.target
    registry_path = script_dir / "evaluation_datasets" / REGISTRY_NAME

    if not target_dir.exists():
        logging.error("No curated folder at %s", target_dir)
        return 1

    try:
        upload(target_dir, args.target, args.scenario, registry_path)
    except Exception as exc:  # noqa: BLE001
        logging.exception("Upload failed: %s", exc)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
