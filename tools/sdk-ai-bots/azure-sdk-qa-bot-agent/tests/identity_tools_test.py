"""Tests for verified GitHub-to-Microsoft identity lookup."""

from types import SimpleNamespace
from unittest.mock import AsyncMock

import httpx
import pytest
from azure.core.exceptions import ClientAuthenticationError

from tools import identity_tools
from tools.identity_tools import IdentityTools


def _link():
    return {
        "github": {"login": "Linked-User"},
        "aad": {
            "id": " 11111111-2222-3333-4444-555555555555 ",
            "preferredName": " Linked Person ",
            "userPrincipalName": "linked.person@microsoft.com",
        },
    }


def _portal(monkeypatch, *, status=200, payload=None, fail=False, headers=None):
    requests = []

    def handle(request):
        requests.append(request)
        if fail:
            raise httpx.ReadTimeout("timeout", request=request)
        return httpx.Response(status, json=payload, headers=headers)

    client_type = httpx.AsyncClient
    monkeypatch.setattr(
        identity_tools.httpx,
        "AsyncClient",
        lambda **kwargs: client_type(transport=httpx.MockTransport(handle), **kwargs),
    )
    credential = SimpleNamespace(
        get_token=AsyncMock(return_value=SimpleNamespace(token="test-token"))
    )
    monkeypatch.setattr(identity_tools, "get_credential", lambda: credential)
    return credential, requests


@pytest.mark.asyncio
async def test_lookup_uses_entra_auth_and_deduplicates_logins(monkeypatch):
    credential, requests = _portal(monkeypatch, payload=_link())

    result = await IdentityTools().resolve_github_users(
        usernames=["@Linked-User", "linked-user"]
    )

    credential.get_token.assert_awaited_once_with(
        "api://2efaf292-00a0-426c-ba7d-f5d2b214b8fc/.default"
    )
    assert len(requests) == 1
    assert requests[0].url.path == "/api/people/links/github/linked-user"
    assert requests[0].headers["authorization"] == "Bearer test-token"
    assert requests[0].headers["api-version"] == "2019-10-01"
    assert requests[0].url.params["showOrganizations"] == "false"
    mention = result.mentions["linked-user"]
    assert result.errors == {}
    assert mention.text == "<at>linked.person@microsoft.com</at>"
    assert mention.mentioned.name == "Linked Person"
    assert mention.mentioned.id == "11111111-2222-3333-4444-555555555555"


@pytest.mark.asyncio
async def test_lookup_supplies_html_safe_microsoft_markup(monkeypatch):
    payload = _link()
    payload["aad"]["userPrincipalName"] = "person&team@microsoft.com"
    _portal(monkeypatch, payload=payload)
    result = await IdentityTools().resolve_github_users(usernames=["linked-user"])
    assert result.mentions["linked-user"].text == "<at>person&amp;team@microsoft.com</at>"


@pytest.mark.asyncio
@pytest.mark.parametrize("status, payload, expected", [
    (404, {}, "No linked Microsoft account"),
    (200, {**_link(), "isServiceAccount": True}, "service account"),
    (401, {}, "HTTP 401"),
    (403, {}, "HTTP 403"),
    (429, {}, "HTTP 429"),
    (500, {}, "HTTP 500"),
])
async def test_unavailable_identity_never_becomes_a_mention(
    monkeypatch, caplog, status, payload, expected
):
    _portal(monkeypatch, status=status, payload=payload)
    result = await IdentityTools().resolve_github_users(usernames=["linked-user"])
    assert result.mentions == {}
    assert expected in result.errors["linked-user"]
    if status not in {200, 404}:
        assert result.errors["linked-user"] in caplog.text


@pytest.mark.asyncio
@pytest.mark.parametrize("payload", [
    [],
    {"github": {"login": "other-user"}, "aad": _link()["aad"]},
    {"github": {"login": "linked-user"}},
    {"github": {"login": "linked-user"}, "aad": {**_link()["aad"], "id": "not-a-guid"}},
    {"github": {"login": "linked-user"}, "aad": {**_link()["aad"], "preferredName": " "}},
    {"github": {"login": "linked-user"}, "aad": {**_link()["aad"], "userPrincipalName": ""}},
])
async def test_invalid_mapping_is_reported_without_exposing_payload(monkeypatch, payload):
    _portal(monkeypatch, payload=payload)
    result = await IdentityTools().resolve_github_users(usernames=["linked-user"])
    assert result.mentions == {}
    assert result.errors["linked-user"] == "The portal returned an invalid Microsoft account mapping."


@pytest.mark.asyncio
async def test_network_failure_is_not_reported_as_an_unlinked_user(monkeypatch):
    _portal(monkeypatch, fail=True)
    result = await IdentityTools().resolve_github_users(usernames=["linked-user"])
    assert result.mentions == {}
    assert "could not be reached" in result.errors["linked-user"]


@pytest.mark.asyncio
async def test_redirects_do_not_forward_portal_credentials(monkeypatch):
    _, requests = _portal(
        monkeypatch, status=302, headers={"location": "https://example.com/sign-in"}
    )
    result = await IdentityTools().resolve_github_users(usernames=["linked-user"])
    assert len(requests) == 1
    assert result.mentions == {}
    assert "HTTP 302" in result.errors["linked-user"]


@pytest.mark.asyncio
async def test_authentication_failure_is_explicit_and_does_not_log_credentials(monkeypatch, caplog):
    credential, requests = _portal(monkeypatch)
    credential.get_token.side_effect = ClientAuthenticationError("sensitive authentication details")
    result = await IdentityTools().resolve_github_users(usernames=["linked-user", "other"])
    assert result.mentions == {}
    assert set(result.errors) == {"linked-user", "other"}
    assert all("Unable to authenticate" in error for error in result.errors.values())
    assert requests == []
    assert "could not acquire a portal token" in caplog.text
    assert "sensitive authentication details" not in caplog.text


@pytest.mark.asyncio
@pytest.mark.parametrize("usernames", [[], ["user"] * 11, ["org/team"], ["../user"], ["a" * 40]])
async def test_invalid_requests_fail_before_authentication(monkeypatch, usernames):
    credential, requests = _portal(monkeypatch)
    with pytest.raises(ValueError):
        await IdentityTools().resolve_github_users(usernames=usernames)
    credential.get_token.assert_not_awaited()
    assert requests == []
