"""Model-written guided replies and backend-only support configuration."""

from __future__ import annotations

import hashlib
import json
import re
from typing import Annotated, Literal

from pydantic import BaseModel, ConfigDict, Field, model_validator

GUIDED_SCHEMA_VERSION = "guided-next-step-v1"
CatalogKey = Annotated[str, Field(pattern=r"^[a-z][a-z0-9_]*$")]
ContactPlaceholder = re.compile(r"\{\{contact:([a-z][a-z0-9_]*)\}\}")


class StrictGuidedModel(BaseModel):
    model_config = ConfigDict(extra="forbid", frozen=True)


class NextStepTarget(StrictGuidedModel):
    contact_id: CatalogKey
    support_areas: tuple[CatalogKey, ...] = Field(min_length=1)


class NextStep(StrictGuidedModel):
    category: Literal["missing_information", "human_action", "human_help"]
    targets: tuple[NextStepTarget, ...]

    @model_validator(mode="after")
    def validate_targets(self) -> NextStep:
        if self.category == "missing_information" and self.targets:
            raise ValueError("Missing information must return to the user, not a contact.")
        contacts = [target.contact_id for target in self.targets]
        areas = [area for target in self.targets for area in target.support_areas]
        if len(contacts) != len(set(contacts)) or len(areas) != len(set(areas)):
            raise ValueError("Group each contact once and request each support area once.")
        return self


class GeneratedGuidedAnswer(StrictGuidedModel):
    schema_version: Literal["guided-next-step-v1"]
    answer: str = Field(min_length=1)
    next_step: NextStep | None

    @model_validator(mode="after")
    def validate_answer(self) -> GeneratedGuidedAnswer:
        if not self.answer.strip():
            raise ValueError("The generated answer must not be blank.")
        return self


class SupportContact(StrictGuidedModel):
    teams_user_id: str | None = Field(default=None, min_length=1)
    maintainer: str | None = Field(default=None, min_length=1)
    notification_enabled: bool = Field(default=False, strict=True)


class SupportArea(StrictGuidedModel):
    key: CatalogKey
    description: str = Field(min_length=1)
    contact_id: CatalogKey

    @model_validator(mode="after")
    def validate_description(self) -> SupportArea:
        if not self.description.strip():
            raise ValueError("A support-area description must not be blank.")
        return self


class SupportContactCatalog(StrictGuidedModel):
    support_areas: tuple[SupportArea, ...]
    contacts: dict[CatalogKey, SupportContact]

    @model_validator(mode="after")
    def validate_catalog(self) -> SupportContactCatalog:
        keys = [area.key for area in self.support_areas]
        if len(keys) != len(set(keys)):
            raise ValueError("Support-area keys must be unique.")
        if any(area.contact_id not in self.contacts for area in self.support_areas):
            raise ValueError("Each support area must reference a configured contact.")
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


def validate_guided_answer(
    generated: GeneratedGuidedAnswer,
    catalog: SupportContactCatalog | None,
) -> None:
    step = generated.next_step
    if step is not None and catalog is None:
        raise ValueError("A guided next step requires this turn's configured support catalog.")
    ownership = {
        area.key: area.contact_id for area in catalog.support_areas
    } if catalog is not None else {}
    targets = step.targets if step is not None else ()
    for target in targets:
        for area in target.support_areas:
            if ownership.get(area) != target.contact_id:
                raise ValueError("The target does not own this area in this turn's catalog.")
    placeholders = ContactPlaceholder.findall(generated.answer)
    residual = ContactPlaceholder.sub("", generated.answer)
    if "{{contact" in residual or len(placeholders) != len(set(placeholders)):
        raise ValueError("Contact placeholders must be well-formed and unique.")
    if set(placeholders) != {target.contact_id for target in targets}:
        raise ValueError("Each selected contact requires exactly one matching placeholder.")
    if catalog is not None and re.search(
        r"<\s*at\b|<\s*mention\b", generated.answer, re.IGNORECASE
    ):
        raise ValueError("Guided previews must not generate active mention markup.")
