"""Read-only account lookup using the Microsoft Open Source portal API."""

from __future__ import annotations

import asyncio
import logging
from html import escape
from typing import Annotated
from uuid import UUID

import httpx
from azure.core.exceptions import ClientAuthenticationError

from models.identity import (
    ResolvedGitHubUser,
    ResolveGitHubUsersResult,
    TeamsUser,
    normalize_github_username,
)
from tools import tool
from utils.azure_credential import get_credential

logger = logging.getLogger(__name__)

_PORTAL_SCOPE = "api://2efaf292-00a0-426c-ba7d-f5d2b214b8fc/.default"
_LINKS_URL = "https://repos.opensource.microsoft.com/api/people/links/github"


class IdentityTools:
    @tool
    async def resolve_github_users(
        self,
        *,
        usernames: Annotated[
            list[str],
            "One to ten GitHub usernames to map to Microsoft accounts for Teams "
            "mentions. Batch all people in one call; do not pass organizations/teams.",
        ],
    ) -> ResolveGitHubUsersResult:
        """Resolve verified contact details or people being directly asked to help or act.

        For plain contact references, copy users[login].plain_text verbatim.
        The UPN is a sign-in address, not a separately verified mailbox.
        Copy users[login].text verbatim only for direct participation requests.
        Resolving an identity alone does not require mentioning it.
        """
        if not 1 <= len(usernames) <= 10:
            raise ValueError("Provide between one and ten GitHub usernames.")
        logins = list(dict.fromkeys(normalize_github_username(name) for name in usernames))

        try:
            token = await get_credential().get_token(_PORTAL_SCOPE)
        except ClientAuthenticationError:
            logger.warning("Microsoft account lookup could not acquire a portal token")
            return ResolveGitHubUsersResult(errors={
                login: "Unable to authenticate to the Microsoft Open Source portal."
                for login in logins
            })

        async with httpx.AsyncClient(
            timeout=8,
            follow_redirects=False,
            headers={
                "Authorization": f"Bearer {token.token}",
                "Accept": "application/json",
                "api-version": "2019-10-01",
            },
        ) as client:
            results = await asyncio.gather(*(
                self._lookup(client, login) for login in logins
            ))
        resolved = ResolveGitHubUsersResult()
        for login, result in zip(logins, results):
            if isinstance(result, str):
                resolved.errors[login] = result
            else:
                resolved.users[login] = result
        return resolved

    @staticmethod
    async def _lookup(
        client: httpx.AsyncClient, login: str
    ) -> ResolvedGitHubUser | str:
        try:
            response = await client.get(
                f"{_LINKS_URL}/{login}", params={"showOrganizations": "false"}
            )
            if response.status_code == 404:
                return "No linked Microsoft account was found."
            response.raise_for_status()
            payload = response.json()
            if not isinstance(payload, dict):
                raise ValueError("Expected a link object.")
            github = payload.get("github")
            if not isinstance(github, dict) or str(github.get("login", "")).lower() != login:
                raise ValueError("The returned GitHub account does not match the request.")
            if payload.get("isServiceAccount") is True:
                return "This GitHub account is a service account, not a person."
            aad = payload.get("aad")
            if not isinstance(aad, dict):
                raise ValueError("No linked Microsoft identity.")
            user = TeamsUser.model_validate({
                "name": aad.get("preferredName"),
                "id": aad.get("id"),
            })
            user.id = str(UUID(user.id.strip()))
            user.name = user.name.strip()
            if not user.name:
                raise ValueError("No Microsoft display name.")
            upn = aad.get("userPrincipalName")
            if not isinstance(upn, str) or not upn.strip():
                raise ValueError("No Microsoft user principal name.")
            return ResolvedGitHubUser(
                text=f"<at>{escape(upn.strip(), quote=False)}</at>",
                mentioned=user,
                plain_text=f"{user.name} ({upn.strip()})",
            )
        except httpx.HTTPStatusError as exc:
            status = exc.response.status_code
            error = f"Microsoft account lookup failed (HTTP {status})."
            if status in {401, 403}:
                error += " The calling Entra identity needs portal access with the links scope."
        except httpx.RequestError:
            error = "Microsoft account lookup failed because the portal could not be reached."
        except ValueError:
            error = "The portal returned an invalid Microsoft account mapping."

        logger.warning("%s", error)
        return error
