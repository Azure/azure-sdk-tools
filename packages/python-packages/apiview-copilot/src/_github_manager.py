# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

from __future__ import annotations

import logging
import time
from typing import Iterable, Optional

import httpx
import jwt
from src._settings import SettingsManager
from src._utils import to_epoch_seconds

logger = logging.getLogger(__name__)


class GithubManager:
    """
    Encapsulates GitHub App auth and common operations (create issues, comments, labels).
    """

    _instance: "GithubManager" = None

    @classmethod
    def get_instance(cls, force_new: bool = False) -> "GithubManager":
        """
        Returns a singleton instance of GithubManager.
        If force_new is True, creates a new instance.
        """
        if cls._instance is None or force_new:
            cls._instance = cls()
        return cls._instance

    def __init__(self):
        settings = SettingsManager()

        self._app_id = int(settings.get("github_app_id"))
        self._client_secret = settings.get("github_app_client_secret")

        self._user_agent = "APIView-Copilot/1.0"
        self._timeout = 10
        self._max_retries = 3
        self._backoff_s = 1.0

        self._client = httpx.Client(
            timeout=self._timeout,
            headers={
                "Accept": "application/vnd.github+json",
                "User-Agent": self._user_agent,
            },
        )

        # token cache: {owner_lower: (token, expires_at_epoch)}
        self._installation_tokens: dict[str, tuple[str, int]] = {}

    def get_oauth_token(self, code: str) -> str:
        """
        Exchange an OAuth authorization code for an access token.
        Returns the access token as a string.
        """
        url = "https://github.com/login/oauth/access_token"
        payload = {
            "client_id": str(self._app_id),
            "client_secret": self._client_secret,
            "code": code,
        }
        headers = {"Accept": "application/json"}
        resp = self._client.post(url, data=payload, headers=headers)
        resp.raise_for_status()
        data = resp.json()
        if "access_token" not in data:
            raise RuntimeError(f"Failed to obtain access token: {data}")
        return data["access_token"]

    def create_issue(
        self,
        *,
        owner: str,
        repo: str,
        title: str,
        body: str,
        labels: Optional[Iterable[str]] = None,
        assignees: Optional[Iterable[str]] = None,
    ) -> dict:
        """
        Creates a new Github issue.
        """
        token = self._get_installation_token(owner)
        url = f"https://api.github.com/repos/{owner}/{repo}/issues"
        payload = {"title": title, "body": body}
        if labels:
            payload["labels"] = list(labels)
        if assignees:
            payload["assignees"] = list(assignees)

        return self._request_json("POST", url, token, json=payload)

    def post_comment_on_issue(self, *, owner: str, repo: str, issue_number: int, body: str) -> dict:
        """
        Posts a comment on a GitHub issue.
        """
        token = self._get_installation_token(owner)
        url = f"https://api.github.com/repos/{owner}/{repo}/issues/{issue_number}/comments"
        return self._request_json("POST", url, token, json={"body": body})

    def add_labels(self, *, owner: str, repo: str, issue_number: int, labels: Iterable[str]) -> dict:
        """
        Adds labels to a GitHub issue.
        """
        token = self._get_installation_token(owner)
        url = f"https://api.github.com/repos/{owner}/{repo}/issues/{issue_number}/labels"
        return self._request_json("POST", url, token, json={"labels": list(labels)})

    def close_issue(self, *, owner: str, repo: str, issue_number: int) -> dict:
        """
        Closes a GitHub issue.
        """
        token = self._get_installation_token(owner)
        url = f"https://api.github.com/repos/{owner}/{repo}/issues/{issue_number}"
        return self._request_json("PATCH", url, token, json={"state": "closed"})

    def _get_installation_token(self, owner: str) -> str:
        owner_l = owner.lower()
        now = int(time.time())
        cached = self._installation_tokens.get(owner_l)
        if cached and cached[1] - 60 > now:  # refresh 1 min early
            return cached[0]

        app_jwt = self._create_app_jwt()
        installation_id = self._find_installation_id(app_jwt, owner)
        token_info = self._create_installation_token(app_jwt, installation_id)
        token = token_info["token"]
        # expires_at format: "2025-10-09T00:12:34Z"
        expires_at_iso = token_info["expires_at"]
        expires_epoch = to_epoch_seconds(expires_at_iso)
        self._installation_tokens[owner_l] = (token, expires_epoch)
        return token

    def _create_app_jwt(self) -> str:
        now = int(time.time())
        payload = {"iat": now - 60, "exp": now + (9 * 60), "iss": self._app_id}
        return jwt.encode(payload, self._private_key_pem, algorithm="RS256")

    def _find_installation_id(self, app_jwt: str, owner: str) -> int:
        # Enumerating installations keeps this class org-agnostic.
        r = self._request_json("GET", "https://api.github.com/app/installations", app_jwt, as_app=True)
        for inst in r:
            acct = inst.get("account") or {}
            if (acct.get("login") or "").lower() == owner.lower():
                return inst["id"]
        raise RuntimeError(f"No GitHub App installation found for owner '{owner}'")

    def _create_installation_token(self, app_jwt: str, installation_id: int) -> dict:
        url = f"https://api.github.com/app/installations/{installation_id}/access_tokens"
        return self._request_json("POST", url, app_jwt, json={}, as_app=True)

    def _request_json(
        self,
        method: str,
        url: str,
        token_or_jwt: str,
        *,
        json: Optional[dict] = None,
        as_app: bool = False,
    ) -> dict | list:
        headers = {}
        if as_app:
            headers["Authorization"] = f"Bearer {token_or_jwt}"
        else:
            headers["Authorization"] = f"token {token_or_jwt}"

        last_exc = None
        for attempt in range(1, self._max_retries + 1):
            try:
                resp = self._client.request(method, url, headers=headers, json=json)
                # Basic handling for secondary rate limiting
                if resp.status_code in (429, 502, 503, 504) or (
                    resp.status_code == 403 and "secondary rate limit" in resp.text.lower()
                ):
                    raise httpx.HTTPStatusError("Retryable", request=resp.request, response=resp)
                resp.raise_for_status()
                if resp.text:
                    return resp.json()
                return {}
            except httpx.HTTPError as exc:
                last_exc = exc
                logger.warning(
                    "GitHub request failed (%s %s) attempt %d/%d: %s", method, url, attempt, self._max_retries, exc
                )
                if attempt == self._max_retries:
                    break
                time.sleep(self._backoff_s * attempt)
        # Exhausted retries
        if isinstance(last_exc, httpx.HTTPStatusError) and last_exc.response is not None:
            try:
                detail = last_exc.response.json()
            except Exception:
                detail = last_exc.response.text
            raise RuntimeError(f"GitHub API error {last_exc.response.status_code}: {detail}") from last_exc
        raise last_exc or RuntimeError("Unknown GitHub request failure")
