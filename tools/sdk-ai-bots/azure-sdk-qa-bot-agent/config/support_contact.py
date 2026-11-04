"""Validated repository support configuration, cached for the backend process."""

from __future__ import annotations

import logging
from functools import lru_cache
from pathlib import Path

import yaml

from models.support_contact import SupportContactCatalog

logger = logging.getLogger(__name__)
SUPPORT_CONTACT_PATH = Path(__file__).resolve().with_name("support-contact.yaml")


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
