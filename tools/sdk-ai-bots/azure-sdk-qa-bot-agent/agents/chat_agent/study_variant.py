"""Opt-in, local-only decision study guidance and startup receipt."""

import hashlib
import json
import os
from pathlib import Path


def sha256(text: str) -> str:
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def load_study_variant(agent_root: Path, root_instructions: str) -> dict | None:
    path = os.environ.get("SDK_QA_STUDY_VARIANT_FILE")
    if not path:
        return None
    receipt = os.environ.get("SDK_QA_STUDY_RECEIPT_FILE")
    if not receipt:
        raise ValueError("SDK_QA_STUDY_RECEIPT_FILE is required for a study variant")
    variant_path = Path(path).resolve(strict=True)
    variant = json.loads(variant_path.read_text(encoding="utf-8"))
    required = {
        "schema_version", "arm", "bundle_sha256", "root_sha256",
        "tenant_sha256", "root_addendum", "tenant_addendum",
    }
    if not isinstance(variant, dict) or set(variant) != required:
        raise ValueError("Invalid study variant fields")
    if variant["schema_version"] != 1 or variant["arm"] not in (
        "baseline", "general", "topic", "combined"
    ):
        raise ValueError("Invalid study variant arm or schema")
    if not all(isinstance(variant[key], str) for key in required - {"schema_version"}):
        raise ValueError("Invalid study variant values")
    tenant_path = agent_root / "prompts" / "tenants" / "api_spec_review.md"
    tenant_instructions = tenant_path.read_text(encoding="utf-8").strip()
    if sha256(root_instructions) != variant["root_sha256"] or (
        sha256(tenant_instructions) != variant["tenant_sha256"]
    ):
        raise ValueError("Study baseline differs from local agent prompts; prepare a new bundle")
    if bool(variant["root_addendum"]) != (variant["arm"] in ("general", "combined")) or (
        bool(variant["tenant_addendum"]) != (variant["arm"] in ("topic", "combined"))
    ):
        raise ValueError("Study guidance does not match the selected arm")
    with Path(receipt).open("x", encoding="utf-8") as stream:
        json.dump({
            "arm": variant["arm"],
            "bundle_sha256": variant["bundle_sha256"],
            "variant_sha256": hashlib.sha256(variant_path.read_bytes()).hexdigest(),
            "pid": os.getpid(),
        }, stream)
    return variant
