"""Repository support-area configuration for the answering agent."""

from __future__ import annotations

import hashlib
import json
from typing import Annotated

from pydantic import BaseModel, ConfigDict, Field, model_validator

from models.identity import GitHubUsername

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
