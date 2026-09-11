from __future__ import annotations

import importlib
import sys
from collections.abc import Iterable, Mapping
from pathlib import Path
from typing import Any


def load_tenant_repository_configs() -> list[tuple[str, Any]]:
    agent_dir = Path(__file__).resolve().parents[2] / "azure-sdk-qa-bot-agent"
    if not agent_dir.is_dir():
        raise RuntimeError(f"QA bot agent directory is unavailable: {agent_dir}")

    sys.path.insert(0, str(agent_dir))
    try:
        try:
            module = importlib.import_module("config.tenant_config")
        except (ImportError, ModuleNotFoundError) as exc:
            raise RuntimeError(
                "Unable to import the QA bot TenantConfig from the sibling "
                "azure-sdk-qa-bot-agent directory."
            ) from exc
        helper = getattr(module, "get_all_code_repository_configs", None)
        if helper is None:
            raise RuntimeError(
                "config.tenant_config.get_all_code_repository_configs is unavailable. "
                "Update azure-sdk-qa-bot-agent to the version that exposes code repository demand."
            )
        raw = helper()
    finally:
        sys.path.remove(str(agent_dir))

    if isinstance(raw, Mapping):
        return [
            (_tenant_id(tenant), config)
            for tenant, configs in raw.items()
            for config in configs
        ]
    if isinstance(raw, Iterable) and not isinstance(raw, (str, bytes)):
        result: list[tuple[str, Any]] = []
        for item in raw:
            if not isinstance(item, tuple) or len(item) != 2:
                raise RuntimeError(
                    "get_all_code_repository_configs must return a mapping or "
                    "an iterable of (tenant_id, CodeRepositoryConfig) pairs."
                )
            result.append((_tenant_id(item[0]), item[1]))
        return result
    raise RuntimeError(
        "get_all_code_repository_configs returned an unsupported value; expected "
        "a mapping or iterable of pairs."
    )


def _tenant_id(value: Any) -> str:
    enum_value = getattr(value, "value", None)
    return str(enum_value if enum_value is not None else value)
