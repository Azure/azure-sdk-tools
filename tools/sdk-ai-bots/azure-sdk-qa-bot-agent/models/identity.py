"""Verified GitHub-to-Microsoft account mappings."""

from __future__ import annotations

from pydantic import BaseModel, Field


class TeamsUser(BaseModel):
    id: str
    name: str


class TeamsMention(BaseModel):
    text: str
    mentioned: TeamsUser


class ResolveGitHubUsersResult(BaseModel):
    mentions: dict[str, TeamsMention] = Field(default_factory=dict)
    errors: dict[str, str] = Field(default_factory=dict)
