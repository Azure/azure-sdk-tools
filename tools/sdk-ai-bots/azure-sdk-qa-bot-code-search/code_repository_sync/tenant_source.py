from __future__ import annotations

import importlib.util
import sys
from collections.abc import Iterable, Mapping
from dataclasses import dataclass
from pathlib import Path
from types import ModuleType
from typing import Any, Callable
from uuid import uuid4


@dataclass(frozen=True)
class _KnowledgeSource:
    name: str
    description: str
    base_url: str = ""
    trim_format: bool = False
    suffix: str = ""
    link_fn: Callable[[str], str] | None = None


def load_tenant_repository_configs(
    tenant_config_path: Path | None = None,
) -> list[tuple[str, Any]]:
    """Load only repository demand from the sibling agent's tenant registry.

    ``tenant_config.py`` imports the agent's full knowledge model, whose
    runtime dependencies are intentionally not part of this offline package.
    A minimal compatible module is installed only for the duration of this
    isolated import.
    """
    path = tenant_config_path or (
        Path(__file__).resolve().parents[2]
        / "azure-sdk-qa-bot-agent"
        / "config"
        / "tenant_config.py"
    )
    if not path.is_file():
        raise RuntimeError(f"QA bot tenant configuration is unavailable: {path}")

    module_name = f"_qa_bot_tenant_config_{uuid4().hex}"
    spec = importlib.util.spec_from_file_location(module_name, path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Unable to load QA bot tenant configuration: {path}")

    models_module = ModuleType("models")
    models_module.__path__ = []  # type: ignore[attr-defined]
    knowledge_module = ModuleType("models.knowledge")
    setattr(knowledge_module, "KnowledgeSource", _KnowledgeSource)
    setattr(knowledge_module, "_trim_file_format", _trim_file_format)
    prior_modules = {
        name: sys.modules.get(name)
        for name in ("models", "models.knowledge", module_name)
    }
    module = importlib.util.module_from_spec(spec)
    try:
        sys.modules["models"] = models_module
        sys.modules["models.knowledge"] = knowledge_module
        sys.modules[module_name] = module
        spec.loader.exec_module(module)
        helper = getattr(module, "get_all_code_repository_configs", None)
        if not callable(helper):
            raise RuntimeError(
                "config.tenant_config.get_all_code_repository_configs is "
                "unavailable."
            )
        raw = helper()
    except Exception as exc:
        if isinstance(exc, RuntimeError):
            raise
        raise RuntimeError(
            f"Unable to load repository declarations from {path}"
        ) from exc
    finally:
        for name, previous in prior_modules.items():
            if previous is None:
                sys.modules.pop(name, None)
            else:
                sys.modules[name] = previous

    return _flatten_repository_configs(raw)


def _flatten_repository_configs(raw: Any) -> list[tuple[str, Any]]:
    if isinstance(raw, Mapping):
        return [
            (_tenant_id(tenant), config)
            for tenant, configs in raw.items()
            for config in _repository_iterable(configs)
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
        "get_all_code_repository_configs returned an unsupported value."
    )


def _repository_iterable(value: Any) -> Iterable[Any]:
    if isinstance(value, Iterable) and not isinstance(value, (str, bytes)):
        return value
    raise RuntimeError("tenant code_repositories must be iterable")


def _tenant_id(value: Any) -> str:
    enum_value = getattr(value, "value", None)
    return str(enum_value if enum_value is not None else value)


def _trim_file_format(path: str) -> str:
    for extension in (".md", ".mdx"):
        if path.endswith(extension):
            path = path[: -len(extension)]
    return path.removeprefix("docs/")
