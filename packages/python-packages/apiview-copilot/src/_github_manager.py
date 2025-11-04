# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

from __future__ import annotations

import base64
import hashlib
import json
import logging
import time
from typing import Iterable, Optional

import httpx
from azure.keyvault.keys import KeyClient
from azure.keyvault.keys.crypto import CryptographyClient, SignatureAlgorithm
from src._credential import get_credential
from src._settings import SettingsManager

logger = logging.getLogger(__name__)


class GithubManager:
    """
    Encapsulates GitHub authentication and common operations (create issues, comments, labels).
    Uses GitHub App authentication only.
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
        self._app_id = settings.get("github_app_id")
        self._installation_tokens: dict[str, tuple[str, int]] = {}

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
        token = self._get_installation_token(owner)
        return self._request_json("POST", url, token, json=payload)
    
    def search_issues(
        self,
        *,
        owner: str,
        repo: str,
        query: str,
        max_results: int = 30,
    ) -> list[dict]:
        """
        Search for issues using GitHub's search API.
        Returns list of issue dicts with number, title, body, created_at, etc.
        """
        search_query = f"repo:{owner}/{repo} {query}"
        url = f"https://api.github.com/search/issues?q={search_query}&per_page={max_results}&sort=updated&order=desc"
        token = self._get_installation_token(owner)
        result = self._request_json("GET", url, token)
        return result.get("items", [])

    def post_comment_on_issue(self, *, owner: str, repo: str, issue_number: int, body: str) -> dict:
        """
        Posts a comment on a GitHub issue.
        """
        url = f"https://api.github.com/repos/{owner}/{repo}/issues/{issue_number}/comments"
        token = self._get_installation_token(owner)
        return self._request_json("POST", url, token, json={"body": body})

    def add_labels(self, *, owner: str, repo: str, issue_number: int, labels: Iterable[str]) -> dict:
        """
        Adds labels to a GitHub issue.
        """
        url = f"https://api.github.com/repos/{owner}/{repo}/issues/{issue_number}/labels"
        token = self._get_installation_token(owner)
        return self._request_json("POST", url, token, json={"labels": list(labels)})

    def close_issue(self, *, owner: str, repo: str, issue_number: int) -> dict:
        url = f"https://api.github.com/repos/{owner}/{repo}/issues/{issue_number}"
        token = self._get_installation_token(owner)
        return self._request_json("PATCH", url, token, json={"state": "closed"})

    def _request_json(
        self,
        method: str,
        url: str,
        token: str,
        *,
        json: Optional[dict] = None,
        as_app: bool = False,
    ) -> dict | list:
        """
        Perform an HTTP request to the GitHub API and return parsed JSON.

        This helper performs retries with exponential backoff and handles
        common transient status codes such as 429/502/503/504 and GitHub's
        secondary rate limiting message. It raises a RuntimeError with
        the remote details if all retries are exhausted.

        Parameters
        - method: HTTP method string (GET, POST, PATCH, etc.)
        - url: The full request URL
        - token: The GitHub installation token to use for Authorization
        - json: Optional request body to send as JSON
        - as_app: Reserved for app-level calls but currently unused

        Returns
        Parsed JSON (dict or list) or empty dict when no JSON body is present.
        """
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

    def _create_app_jwt(self) -> str:
        """
        Mint a GitHub App JWT using Azure Key Vault's asymmetric signing key.

        This creates a short-lived RS256 JWT signed by a Key Vault key that
        represents the GitHub App's private key. The token will be valid for
        up to 10 minutes as recommended by GitHub.

        Returns
        The signed JWT string to be used as an Authorization bearer token.
        """
        settings = SettingsManager()
        keyvault_url = settings.get("github_app_keyvault_url")
        key_name = settings.get("github_app_key_name")
        app_id = settings.get("github_app_id")

        credential = get_credential()
        key_client = KeyClient(vault_url=keyvault_url, credential=credential)
        key = key_client.get_key(key_name)
        crypto_client = CryptographyClient(key, credential=credential)

        header = {"alg": "RS256", "typ": "JWT"}
        now = int(time.time())
        payload = {"iat": now, "exp": now + 600, "iss": app_id}

        def b64url(data):
            raw = json.dumps(data, separators=(",", ":")).encode("utf-8")
            b64 = base64.urlsafe_b64encode(raw).rstrip(b"=")
            return b64.decode("utf-8")

        encoded_header = b64url(header)
        encoded_payload = b64url(payload)
        unsigned_token = f"{encoded_header}.{encoded_payload}"

        digest_bytes = hashlib.sha256(unsigned_token.encode("ascii")).digest()
        sign_result = crypto_client.sign(SignatureAlgorithm.rs256, digest_bytes)
        signature = base64.urlsafe_b64encode(sign_result.signature).rstrip(b"=").decode("utf-8")

        jwt = f"{unsigned_token}.{signature}"
        return jwt

    def _find_installation_id(self, app_jwt: str, owner: str) -> int:
        """
        Find the GitHub App installation id for the given repository owner.

        The function enumerates the installations visible to the App and
        returns the installation id for the matching owner login (case-insensitive).
        """
        url = "https://api.github.com/app/installations"
        headers = {
            "Authorization": f"Bearer {app_jwt}",
            "Accept": "application/vnd.github+json",
            "User-Agent": self._user_agent,
            "X-GitHub-Api-Version": "2022-11-28",
        }
        resp = self._client.get(url, headers=headers)
        resp.raise_for_status()
        installations = resp.json()
        for inst in installations:
            acct = inst.get("account") or {}
            if (acct.get("login") or "").lower() == owner.lower():
                return inst["id"]
        raise RuntimeError(f"No GitHub App installation found for owner '{owner}'")

    def _create_installation_token(self, app_jwt: str, installation_id: int) -> dict:
        """
        Exchange a GitHub App JWT for an installation access token.

        Parameters
        - app_jwt: The App JWT obtained from _create_app_jwt
        - installation_id: The installation ID for the target owner

        Returns
        The JSON object returned by GitHub containing the token and metadata.
        """
        url = f"https://api.github.com/app/installations/{installation_id}/access_tokens"
        headers = {
            "Authorization": f"Bearer {app_jwt}",
            "Accept": "application/vnd.github+json",
            "User-Agent": self._user_agent,
            "X-GitHub-Api-Version": "2022-11-28",
        }
        resp = self._client.post(url, headers=headers, json={})
        resp.raise_for_status()
        return resp.json()

    def _get_installation_token(self, owner: str) -> str:
        """
        Obtain an installation token for the specified owner using the App.

        This orchestration method creates a temporary App JWT, finds the
        installation id, exchanges it for a token, and returns the token
        string to be used in subsequent API calls.
        """
        app_jwt = self._create_app_jwt()
        installation_id = self._find_installation_id(app_jwt, owner)
        token_info = self._create_installation_token(app_jwt, installation_id)
        return token_info["token"]
