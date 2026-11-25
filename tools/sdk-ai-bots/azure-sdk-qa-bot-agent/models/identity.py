"""Verified GitHub-to-Microsoft account mappings."""

from __future__ import annotations

import re
from typing import Annotated

from pydantic import AfterValidator, BaseModel, ConfigDict, Field

_LOGIN_RE = re.compile(r"[A-Za-z0-9]+(?:-[A-Za-z0-9]+)*")


def normalize_github_username(value: str) -> str:
    login = value.strip().removeprefix("@").lower()
    if len(login) > 39 or not _LOGIN_RE.fullmatch(login):
        raise ValueError("Each username must be a valid individual GitHub login.")
    return login


GitHubUsername = Annotated[str, AfterValidator(normalize_github_username)]


class TeamsUser(BaseModel):
    id: str
    name: str


class TeamsMention(BaseModel):
    text: str
    mentioned: TeamsUser


class ResolvedGitHubUser(TeamsMention):
    plain_text: str


class ResolveGitHubUsersResult(BaseModel):
    model_config = ConfigDict(extra="forbid")

    users: dict[str, ResolvedGitHubUser] = Field(default_factory=dict)
    errors: dict[str, str] = Field(default_factory=dict)
