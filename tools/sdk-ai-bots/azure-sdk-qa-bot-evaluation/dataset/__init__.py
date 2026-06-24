"""Dataset preparation package for the Azure SDK QA bot evaluation.

This package is the *dataset preparation* half of the evaluation system. It is
independent of the evaluation run: it scans storage, screens cases into curated
per-scenario JSONL files, and uploads them as Foundry versioned Dataset assets.

Modules:
    schema   - canonical JSONL schema + validator
    curate   - scan all blob MD, normalize to schema, incremental dedup -> staging
    review   - promote reviewed=="pass" rows into evaluation_datasets/{basic,perf}/<scenario>.jsonl
    upload   - upload per-scenario JSONL as a Foundry versioned Dataset asset
    online_snapshot - download recent (rolling-window) MD -> per-scenario JSONL (online eval)
    validate - CLI entry for schema validation
"""

from .schema import (
    CanonicalCase,
    ValidationError,
    iter_jsonl,
    validate_case,
    validate_file,
)

__all__ = [
    "CanonicalCase",
    "ValidationError",
    "iter_jsonl",
    "validate_case",
    "validate_file",
]
