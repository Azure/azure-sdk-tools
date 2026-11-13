"""Render answer placeholders using verified Microsoft account mappings."""

from __future__ import annotations

import asyncio
import logging
import re
from html import escape

import httpx
from azure.core.exceptions import AzureError

from models.identity import ResolveGitHubUsersResult, TeamsMention, normalize_github_username
from tools.identity_tools import IdentityTools

logger = logging.getLogger(__name__)

MAX_RESOLVED_USERS = 10
RESOLUTION_TIMEOUT_SECONDS = 10

_TOKEN_RE = re.compile(
    r"(?<!\\)\{\{(?P<kind>mention|contact):(?P<login>[^{}\r\n]*)\}\}",
)


async def resolve_answer_mentions(
    answer: str, *, enabled: bool
) -> tuple[str, list[TeamsMention] | None]:
    """Resolve placeholders regardless of Markdown formatting, for at most ten people.

    Returns the rendered answer and deduplicated Teams mention metadata.
    Only enabled mention placeholders notify; otherwise the metadata is None.
    """
    logins: dict[str, str] = {}
    for match in _TOKEN_RE.finditer(answer):
        raw = match.group("login")
        if raw in logins:
            continue
        try:
            logins[raw] = normalize_github_username(raw)
        except ValueError:
            logger.warning("Invalid GitHub username in an answer identity placeholder")

    usernames = list(dict.fromkeys(logins.values()))
    if len(usernames) > MAX_RESOLVED_USERS:
        logger.warning(
            "Answer identity lookup limited to %d users; remaining handles stay plain",
            MAX_RESOLVED_USERS,
        )
    usernames = usernames[:MAX_RESOLVED_USERS]
    identities = ResolveGitHubUsersResult()
    if usernames:
        try:
            identities = await asyncio.wait_for(
                IdentityTools().resolve_github_users(usernames=usernames),
                timeout=RESOLUTION_TIMEOUT_SECONDS,
            )
        except (TimeoutError, AzureError, httpx.RequestError) as exc:
            logger.warning(
                "Backend answer identity lookup failed (%s); keeping GitHub handles",
                type(exc).__name__,
            )
        for login in usernames:
            if login not in identities.users or login in identities.errors:
                logger.warning(
                    "Could not resolve answer identity %s: %s",
                    login, identities.errors.get(login, "No verified mapping returned."),
                )

    mentions: list[TeamsMention] = []

    def replace(match: re.Match[str]) -> str:
        raw = match.group("login")
        login = logins.get(raw)
        user = identities.users.get(login) if login is not None and login in usernames else None
        if user is None or login in identities.errors:
            return "@" + escape(
                login or raw.strip().removeprefix("@"), quote=False,
            )
        if match.group("kind") == "contact" or not enabled:
            return escape(user.plain_text, quote=False)
        mention = TeamsMention(text=user.text, mentioned=user.mentioned)
        if mention not in mentions:
            mentions.append(mention)
        return user.text

    return _TOKEN_RE.sub(replace, answer), mentions or None
