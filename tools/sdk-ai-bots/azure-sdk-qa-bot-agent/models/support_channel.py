"""Validated support-channel configuration and cached agent-context serialization."""

from __future__ import annotations

import logging
from functools import lru_cache
from pathlib import Path
from typing import Annotated

import yaml
from pydantic import BaseModel, ConfigDict, Field, model_validator

logger = logging.getLogger(__name__)
SUPPORT_CHANNEL_PATH = (
    Path(__file__).resolve().parents[1] / "config" / "support-channel.yaml"
)

CatalogKey = Annotated[str, Field(pattern=r"^[a-z][a-z0-9_]*$")]


class SupportChannelModel(BaseModel):
    model_config = ConfigDict(extra="forbid")


class SupportChannel(SupportChannelModel):
    key: CatalogKey
    name: str = Field(min_length=1)
    description: str = Field(min_length=1)
    link: str = Field(min_length=1)

    @model_validator(mode="after")
    def validate_channel(self) -> SupportChannel:
        if not self.name.strip():
            raise ValueError("A support-channel name must not be blank.")
        if not self.description.strip():
            raise ValueError("A support-channel description must not be blank.")
        if not self.link.strip().startswith("https://teams.microsoft.com/l/channel/"):
            raise ValueError("A support-channel link must be a Teams channel URL.")
        return self


class SupportChannelCatalog(SupportChannelModel):
    support_channels: list[SupportChannel]

    @model_validator(mode="after")
    def validate_catalog(self) -> SupportChannelCatalog:
        keys = [ch.key for ch in self.support_channels]
        if len(keys) != len(set(keys)):
            raise ValueError("Support-channel keys must be unique.")
        names = [ch.name.strip().casefold() for ch in self.support_channels]
        if len(names) != len(set(names)):
            raise ValueError("Support-channel names must be unique.")
        return self

    def format_markdown(self) -> str:
        return "\n".join(
            f"- `{ch.key}` (**{ch.name}**) — {ch.description}"
            for ch in self.support_channels
        )


class _UniqueKeyLoader(yaml.SafeLoader):
    pass


def _unique_mapping(loader: _UniqueKeyLoader, node: yaml.MappingNode) -> dict:
    result = {}
    for key_node, value_node in node.value:
        key = loader.construct_object(key_node)
        if not isinstance(key, str) or key in result:
            raise yaml.constructor.ConstructorError(
                None,
                None,
                "Configuration keys must be unique strings.",
                key_node.start_mark,
            )
        result[key] = loader.construct_object(value_node)
    return result


_UniqueKeyLoader.add_constructor(
    yaml.resolver.BaseResolver.DEFAULT_MAPPING_TAG, _unique_mapping
)


def parse_support_channel_catalog(data: bytes) -> SupportChannelCatalog:
    if not data:
        raise ValueError("Support-channel configuration is empty or missing.")
    return SupportChannelCatalog.model_validate(
        yaml.load(data.decode("utf-8"), Loader=_UniqueKeyLoader)
    )


@lru_cache(maxsize=1)
def get_support_channel_catalog() -> SupportChannelCatalog | None:
    """Cache validated support-channel catalog, or None if unavailable."""
    try:
        catalog = parse_support_channel_catalog(SUPPORT_CHANNEL_PATH.read_bytes())
    except (OSError, ValueError, yaml.YAMLError) as exc:
        logger.error(
            "Support-channel configuration failed; continuing without channel routing catalog: path=%s error=%s",
            SUPPORT_CHANNEL_PATH,
            type(exc).__name__,
        )
        return None
    logger.info(
        "Support-channel catalog loaded: channels=%d",
        len(catalog.support_channels),
    )
    return catalog


def get_support_channel(target: str | None) -> SupportChannel | None:
    """Resolve a configured support channel by key or case-insensitive name."""
    if not target or not target.strip():
        return None
    catalog = get_support_channel_catalog()
    if catalog is None:
        return None
    normalized = target.strip().casefold()
    for ch in catalog.support_channels:
        if ch.key.casefold() == normalized or ch.name.strip().casefold() == normalized:
            return ch
    return None
