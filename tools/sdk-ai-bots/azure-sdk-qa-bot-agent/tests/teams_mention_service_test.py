"""Backend rendering of explicit, model-written identity placeholders."""

import asyncio
from unittest.mock import AsyncMock

import httpx
import pytest
from azure.core.exceptions import ClientAuthenticationError, ServiceRequestError

from models.identity import ResolvedGitHubUser, ResolveGitHubUsersResult, TeamsMention, TeamsUser
from services import teams_mention_service as service
from services.teams_mention_service import resolve_answer_mentions


def resolved_user(login="reviewer-a"):
    return ResolvedGitHubUser(
        text=f"<at>{login}@example.test</at>",
        mentioned=TeamsUser(
            id="11111111-2222-3333-4444-555555555555", name="Reviewer",
        ),
        plain_text=f"Reviewer ({login}@example.test)",
    )


@pytest.fixture
def lookup(monkeypatch):
    resolve = AsyncMock(return_value=ResolveGitHubUsersResult(
        users={"reviewer-a": resolved_user()},
    ))
    monkeypatch.setattr(service.IdentityTools, "resolve_github_users", resolve)
    return resolve


@pytest.mark.asyncio
async def test_direct_and_contact_placeholders_share_one_normalized_lookup(lookup):
    user = resolved_user()
    answer, mentions = await resolve_answer_mentions(
        "Contact: {{contact: Reviewer-A }}. "
        "{{mention:@REVIEWER-A}}, please review the PR. "
        "{{mention:reviewer-a}}, please confirm.",
        enabled=True,
    )
    lookup.assert_awaited_once_with(usernames=["reviewer-a"])
    assert answer == (
        f"Contact: {user.plain_text}. {user.text}, please review the PR. "
        f"{user.text}, please confirm."
    )
    assert mentions == [TeamsMention(text=user.text, mentioned=user.mentioned)]


@pytest.mark.asyncio
@pytest.mark.parametrize("enabled", [True, False])
async def test_contact_placeholders_never_notify(lookup, enabled):
    user = resolved_user()
    answer, mentions = await resolve_answer_mentions(
        "For questions, contact {{contact:reviewer-a}}.", enabled=enabled,
    )
    assert answer == f"For questions, contact {user.plain_text}."
    assert mentions is None


@pytest.mark.asyncio
async def test_disabled_mentions_render_plain_contacts(lookup):
    answer, mentions = await resolve_answer_mentions(
        "{{mention:reviewer-a}}, please help.", enabled=False,
    )
    lookup.assert_awaited_once_with(usernames=["reviewer-a"])
    assert answer == f"{resolved_user().plain_text}, please help."
    assert "<at>" not in answer
    assert mentions is None


@pytest.mark.asyncio
@pytest.mark.parametrize("answer", [
    "Hello, @reviewer-a.",
    "<at>unverified@example.test</at>, please help.",
    "`{{mention:reviewer-a}}`",
    "``{{mention:reviewer-a}}``",
    "```text\n{{mention:reviewer-a}}\n```",
    "````text\n`code` {{mention:reviewer-a}}\n````",
    "~~~text\n{{contact:reviewer-a}}\n~~~",
    "```text\n{{mention:reviewer-a}}",
    "> {{mention:reviewer-a}}, please help.",
    "\\{{mention:reviewer-a}}",
])
async def test_plain_handles_unverified_tags_and_literals_do_not_trigger_lookup(lookup, answer):
    rendered, mentions = await resolve_answer_mentions(answer, enabled=True)
    assert rendered == answer
    assert mentions is None
    lookup.assert_not_awaited()


@pytest.mark.asyncio
async def test_literals_do_not_hide_a_direct_request_elsewhere(lookup):
    literal = "```text\n{{mention:another-user}}\n```\n> {{contact:quoted-user}}\n"
    answer, mentions = await resolve_answer_mentions(
        literal + "{{mention:reviewer-a}}, please help.", enabled=True,
    )
    assert answer == literal + resolved_user().text + ", please help."
    assert mentions is not None and len(mentions) == 1
    lookup.assert_awaited_once_with(usernames=["reviewer-a"])


@pytest.mark.asyncio
async def test_partial_results_keep_failed_handles_and_log_failures(lookup, caplog):
    lookup.return_value.errors["reviewer-b"] = "No linked Microsoft account was found."
    answer, mentions = await resolve_answer_mentions(
        "{{mention:reviewer-a}} and {{mention:reviewer-b}}, please review. "
        "Contact {{contact:missing-user}}.",
        enabled=True,
    )
    assert answer == (
        f"{resolved_user().text} and @reviewer-b, please review. Contact @missing-user."
    )
    assert mentions is not None and len(mentions) == 1
    assert "No linked Microsoft account was found." in caplog.text
    assert "No verified mapping returned." in caplog.text


@pytest.mark.asyncio
async def test_error_for_a_returned_user_prevents_a_notification(lookup):
    lookup.return_value.errors["reviewer-a"] = "Mapping could not be verified."
    answer, mentions = await resolve_answer_mentions(
        "{{mention:reviewer-a}}, please review.", enabled=True,
    )
    assert answer == "@reviewer-a, please review."
    assert mentions is None


@pytest.mark.asyncio
@pytest.mark.parametrize("raw", ["org/team", "../user", "-user", "a" * 40, "a--b", "<at>forged</at>"])
async def test_invalid_usernames_never_reach_the_portal(lookup, caplog, raw):
    answer, mentions = await resolve_answer_mentions(
        "{{mention:" + raw + "}}, please help.", enabled=True,
    )
    lookup.assert_not_awaited()
    assert "{{mention:" not in answer
    assert "<at>" not in answer
    assert mentions is None
    assert "Invalid GitHub username" in caplog.text


@pytest.mark.asyncio
async def test_excess_users_fall_back_to_plain_handles(lookup, caplog):
    logins = [f"reviewer-{index}" for index in range(12)]
    lookup.return_value = ResolveGitHubUsersResult(
        users={login: resolved_user(login) for login in logins},
    )
    answer, mentions = await resolve_answer_mentions(
        ", ".join("{{mention:" + login + "}}" for login in logins), enabled=True,
    )
    lookup.assert_awaited_once_with(usernames=logins[:10])
    assert mentions is not None and len(mentions) == 10
    assert answer.endswith("@reviewer-10, @reviewer-11")
    assert "limited to 10 users" in caplog.text


@pytest.mark.asyncio
async def test_multiple_github_logins_for_one_identity_deduplicate_metadata(lookup):
    lookup.return_value.users["other-login"] = resolved_user()
    answer, mentions = await resolve_answer_mentions(
        "{{mention:reviewer-a}} and {{mention:other-login}}, please help.",
        enabled=True,
    )
    assert answer.count(resolved_user().text) == 2
    assert mentions is not None and len(mentions) == 1


@pytest.mark.asyncio
async def test_plain_contact_is_html_safe(lookup):
    lookup.return_value.users["reviewer-a"].plain_text = "<at>not-a-tag</at> & Reviewer"
    answer, mentions = await resolve_answer_mentions(
        "{{contact:reviewer-a}}", enabled=True,
    )
    assert answer == "&lt;at&gt;not-a-tag&lt;/at&gt; &amp; Reviewer"
    assert mentions is None


@pytest.mark.asyncio
@pytest.mark.parametrize("error", [
    ClientAuthenticationError("sensitive auth details"),
    ServiceRequestError("sensitive network details"),
    httpx.ConnectError("sensitive connection details"),
    TimeoutError("sensitive timeout details"),
])
async def test_expected_lookup_failures_preserve_the_answer(lookup, caplog, error):
    lookup.side_effect = error
    answer, mentions = await resolve_answer_mentions(
        "{{mention:reviewer-a}}, please help; contact {{contact:reviewer-b}}.",
        enabled=True,
    )
    assert answer == "@reviewer-a, please help; contact @reviewer-b."
    assert mentions is None
    assert "Backend answer identity lookup failed" in caplog.text
    assert "sensitive" not in caplog.text


@pytest.mark.asyncio
async def test_timeout_covers_the_entire_lookup_including_token_acquisition(
    monkeypatch, lookup, caplog,
):
    cancelled = asyncio.Event()

    async def slow_lookup(**kwargs):
        try:
            await asyncio.sleep(10)
        finally:
            cancelled.set()

    lookup.side_effect = slow_lookup
    monkeypatch.setattr(service, "RESOLUTION_TIMEOUT_SECONDS", 0.01)
    answer, mentions = await resolve_answer_mentions(
        "{{mention:reviewer-a}}, please help.", enabled=True,
    )
    assert answer == "@reviewer-a, please help."
    assert mentions is None
    assert cancelled.is_set()
    assert "TimeoutError" in caplog.text


@pytest.mark.asyncio
async def test_request_cancellation_is_not_swallowed(lookup):
    lookup.side_effect = asyncio.CancelledError
    with pytest.raises(asyncio.CancelledError):
        await resolve_answer_mentions("{{mention:reviewer-a}}", enabled=True)
