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
from azure.identity import DefaultAzureCredential
from azure.keyvault.keys import KeyClient
from azure.keyvault.keys.crypto import CryptographyClient, SignatureAlgorithm
from src._settings import SettingsManager

logger = logging.getLogger(__name__)


class GithubManager:
    """
    Encapsulates GitHub authentication and common operations (create issues, comments, labels).
    Uses GitHub App authentication by default, with option to use PAT.
    """

    _instance: "GithubManager" = None

    @classmethod
    def get_instance(cls, force_new: bool = False, use_app_auth: bool = True) -> "GithubManager":
        """
        Returns a singleton instance of GithubManager.
        If force_new is True, creates a new instance.
        """
        if cls._instance is None or force_new:
            cls._instance = cls(use_app_auth=use_app_auth)
        return cls._instance

    def __init__(self, use_app_auth: bool = True):
        settings = SettingsManager()
        self._pat = settings.get("azuresdk-github-pat")
        self._user_agent = "APIView-Copilot/1.0"
        self._timeout = 10
        self._max_retries = 3
        self._backoff_s = 1.0
        self.use_app_auth = use_app_auth

        self._client = httpx.Client(
            timeout=self._timeout,
            headers={
                "Accept": "application/vnd.github+json",
                "User-Agent": self._user_agent,
            },
        )

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
        use_app_auth: Optional[bool] = None,
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
        use_app = self.use_app_auth if use_app_auth is None else use_app_auth
        token = self._get_installation_token(owner) if use_app else self._pat
        return self._request_json("POST", url, token, json=payload)

    def post_comment_on_issue(
        self, *, owner: str, repo: str, issue_number: int, body: str, use_app_auth: Optional[bool] = None
    ) -> dict:
        """
        Posts a comment on a GitHub issue.
        """
        url = f"https://api.github.com/repos/{owner}/{repo}/issues/{issue_number}/comments"
        use_app = self.use_app_auth if use_app_auth is None else use_app_auth
        token = self._get_installation_token(owner) if use_app else self._pat
        return self._request_json("POST", url, token, json={"body": body})

    def add_labels(
        self, *, owner: str, repo: str, issue_number: int, labels: Iterable[str], use_app_auth: Optional[bool] = None
    ) -> dict:
        """
        Adds labels to a GitHub issue.
        """
        url = f"https://api.github.com/repos/{owner}/{repo}/issues/{issue_number}/labels"
        use_app = self.use_app_auth if use_app_auth is None else use_app_auth
        token = self._get_installation_token(owner) if use_app else self._pat
        return self._request_json("POST", url, token, json={"labels": list(labels)})

    def close_issue(self, *, owner: str, repo: str, issue_number: int, use_app_auth: Optional[bool] = None) -> dict:
        """
        Closes a GitHub issue.
        """
        url = f"https://api.github.com/repos/{owner}/{repo}/issues/{issue_number}"
        use_app = self.use_app_auth if use_app_auth is None else use_app_auth
        token = self._get_installation_token(owner) if use_app else self._pat
        return self._request_json("PATCH", url, token, json={"state": "closed"})

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

    # === GitHub App authentication ===

    def _create_app_jwt(self) -> str:
        """
        Mint a GitHub App JWT using Azure Key Vault 'sign' (non-exportable key).
        """
        settings = SettingsManager()
        keyvault_url = settings.get("github_app_keyvault_url")
        key_name = settings.get("github_app_key_name")
        app_id = settings.get("github_app_id")

        credential = DefaultAzureCredential()
        key_client = KeyClient(vault_url=keyvault_url, credential=credential)
        key = key_client.get_key(key_name)
        crypto_client = CryptographyClient(key, credential=credential)

        # JWT header and payload
        header = {"alg": "RS256", "typ": "JWT"}
        now = int(time.time())
        payload = {"iat": now, "exp": now + 600, "iss": app_id}  # 10 minutes

        def b64url(data):
            raw = json.dumps(data, separators=(",", ":")).encode("utf-8")
            b64 = base64.urlsafe_b64encode(raw).rstrip(b"=")
            return b64.decode("utf-8")

        encoded_header = b64url(header)
        encoded_payload = b64url(payload)
        unsigned_token = f"{encoded_header}.{encoded_payload}"

        # Compute SHA-256 digest of the unsigned token
        digest_bytes = hashlib.sha256(unsigned_token.encode("ascii")).digest()

        # Sign the digest using Key Vault
        sign_result = crypto_client.sign(SignatureAlgorithm.rs256, digest_bytes)
        signature = base64.urlsafe_b64encode(sign_result.signature).rstrip(b"=").decode("utf-8")

        jwt = f"{unsigned_token}.{signature}"
        return jwt

    def _find_installation_id(self, app_jwt: str, owner: str) -> int:
        """
        Find the installation ID for the given owner using the App JWT.
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
        Exchange the App JWT and installation ID for an installation access token.
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
        Get a fresh installation token for the given owner using GitHub App authentication.
        """
        app_jwt = self._create_app_jwt()
        installation_id = self._find_installation_id(app_jwt, owner)
        token_info = self._create_installation_token(app_jwt, installation_id)
        return token_info["token"]
