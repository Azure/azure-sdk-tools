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
from src._settings import SettingsManager

logger = logging.getLogger(__name__)


class GithubManager:
    """
    Encapsulates GitHub authentication and common operations (create issues, comments, labels).
    Uses a Personal Access Token (PAT) for authentication.
    GitHub App logic is stubbed for future use.
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
        self._pat = settings.get("azuresdk-github-pat")
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

        # Stubs for future GitHub App support
        self._app_id = settings.get("github_app_id")
        self._client_secret = settings.get("github_app_client_secret")
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

    # === GitHub App stubs (not used) ===
    def _get_installation_token(self, owner: str) -> str:
        raise NotImplementedError("GitHub App authentication is not currently used.")

    def _create_app_jwt(self) -> str:
        raise NotImplementedError("GitHub App authentication is not currently used.")

    def _find_installation_id(self, app_jwt: str, owner: str) -> int:
        raise NotImplementedError("GitHub App authentication is not currently used.")

    def _create_installation_token(self, app_jwt: str, installation_id: int) -> dict:
        raise NotImplementedError("GitHub App authentication is not currently used.")

    # === PAT-based API operations ===

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
        url = f"https://api.github.com/repos/{owner}/{repo}/issues"
        payload = {"title": title, "body": body}
        if labels:
            payload["labels"] = list(labels)
        if assignees:
            payload["assignees"] = list(assignees)
        return self._request_json("POST", url, self._pat, json=payload)

    def post_comment_on_issue(self, *, owner: str, repo: str, issue_number: int, body: str) -> dict:
        """
        Posts a comment on a GitHub issue.
        """
        url = f"https://api.github.com/repos/{owner}/{repo}/issues/{issue_number}/comments"
        return self._request_json("POST", url, self._pat, json={"body": body})

    def add_labels(self, *, owner: str, repo: str, issue_number: int, labels: Iterable[str]) -> dict:
        """
        Adds labels to a GitHub issue.
        """
        url = f"https://api.github.com/repos/{owner}/{repo}/issues/{issue_number}/labels"
        return self._request_json("POST", url, self._pat, json={"labels": list(labels)})

    def close_issue(self, *, owner: str, repo: str, issue_number: int) -> dict:
        """
        Closes a GitHub issue.
        """
        url = f"https://api.github.com/repos/{owner}/{repo}/issues/{issue_number}"
        return self._request_json("PATCH", url, self._pat, json={"state": "closed"})

    def _request_json(
        self,
        method: str,
        url: str,
        token: str,
        *,
        json: Optional[dict] = None,
        as_app: bool = False,  # ignored for PAT
    ) -> dict | list:
        headers = {"Authorization": f"token {token}"}
        last_exc = None
        for attempt in range(1, self._max_retries + 1):
            try:
                resp = self._client.request(method, url, headers=headers, json=json)
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
        if isinstance(last_exc, httpx.HTTPStatusError) and last_exc.response is not None:
            try:
                detail = last_exc.response.json()
            except Exception:
                detail = last_exc.response.text
            raise RuntimeError(f"GitHub API error {last_exc.response.status_code}: {detail}") from last_exc
        raise last_exc or RuntimeError("Unknown GitHub request failure")
