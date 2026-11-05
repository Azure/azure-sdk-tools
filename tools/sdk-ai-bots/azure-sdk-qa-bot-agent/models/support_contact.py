"""Validated repository support configuration and cached catalog loading."""

from __future__ import annotations

import hashlib
import json
import logging
from functools import lru_cache
from pathlib import Path
from typing import Annotated

import yaml
from pydantic import BaseModel, ConfigDict, Field, model_validator

from models.identity import GitHubUsername

logger = logging.getLogger(__name__)
SUPPORT_CONTACT_PATH = Path(__file__).resolve().parents[1] / "config" / "support-contact.yaml"

CatalogKey = Annotated[str, Field(pattern=r"^[a-z][a-z0-9_]*$")]


class SupportModel(BaseModel):
    model_config = ConfigDict(extra="forbid", frozen=True)


class SupportArea(SupportModel):
    key: CatalogKey
    description: str = Field(min_length=1)
    github_username: GitHubUsername

    @model_validator(mode="after")
    def validate_description(self) -> SupportArea:
        if not self.description.strip():
            raise ValueError("A support-area description must not be blank.")
        return self


class SupportContactCatalog(SupportModel):
    support_areas: tuple[SupportArea, ...]

    @model_validator(mode="after")
    def validate_catalog(self) -> SupportContactCatalog:
        keys = [area.key for area in self.support_areas]
        if len(keys) != len(set(keys)):
            raise ValueError("Support-area keys must be unique.")
        return self

    @property
    def revision(self) -> str:
        return hashlib.sha256(self.model_dump_json().encode("utf-8")).hexdigest()

    def model_context(self) -> str:
        return "[support_catalog]\n" + json.dumps(
            {
                "revision": self.revision,
                "support_areas": [area.model_dump() for area in self.support_areas],
            },
            ensure_ascii=True,
            separators=(",", ":"),
        )


class _UniqueKeyLoader(yaml.SafeLoader):
    pass


def _unique_mapping(loader: _UniqueKeyLoader, node: yaml.MappingNode) -> dict:
    result = {}
    for key_node, value_node in node.value:
        key = loader.construct_object(key_node)
        if not isinstance(key, str) or key in result:
            raise yaml.constructor.ConstructorError(
                None, None, "Configuration keys must be unique strings.", key_node.start_mark
            )
        result[key] = loader.construct_object(value_node)
    return result


_UniqueKeyLoader.add_constructor(
    yaml.resolver.BaseResolver.DEFAULT_MAPPING_TAG, _unique_mapping
)


def parse_support_catalog(data: bytes) -> SupportContactCatalog:
    if not data:
        raise ValueError("Support-contact configuration is empty or missing.")
    return SupportContactCatalog.model_validate(
        yaml.load(data.decode("utf-8"), Loader=_UniqueKeyLoader)
    )


@lru_cache(maxsize=1)
def get_support_catalog() -> SupportContactCatalog | None:
    try:
        catalog = parse_support_catalog(SUPPORT_CONTACT_PATH.read_bytes())
    except (OSError, ValueError, yaml.YAMLError) as exc:
        logger.error(
            "Support-contact configuration failed: path=%s error=%s",
            SUPPORT_CONTACT_PATH, type(exc).__name__,
        )
        raise RuntimeError("Unable to load valid support-contact configuration.") from exc
    logger.info(
        "Support-contact catalog loaded: revision=%s areas=%d",
        catalog.revision, len(catalog.support_areas),
    )
    return catalog if catalog.support_areas else None
