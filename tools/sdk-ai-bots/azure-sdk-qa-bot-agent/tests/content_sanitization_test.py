"""Unit tests for content sanitization, sensitivity gating, and log redaction."""

from __future__ import annotations

import json
import sys
from datetime import datetime, timezone
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import AsyncMock

import pytest

_PROJECT_ROOT = str(Path(__file__).resolve().parent.parent)
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

from middleware.issue_publication_guard import IssuePublicationGuardMiddleware
from utils.content_sanitization import (
    LogSanitizationFilter,
    install_log_sanitization,
    is_public_github_repo,
    mask_identifier,
    sanitize_issue_text,
    sanitize_log_text,
)


def _make_context(tool_name: str, arguments: dict, session=None):
    return SimpleNamespace(
        function=SimpleNamespace(name=tool_name),
        arguments=arguments,
        session=session,
        result=None,
    )


def test_sanitize_issue_text_redacts_secrets_and_ignores_trace_ids():
    out_secret, _ = sanitize_issue_text(
        "token = 'ghp_1234567890abcdefghijklmnopqrstuvwxyzABCD'",
        is_public_target=True,
    )
    assert "ghp_" not in out_secret
    assert "1234567890abcdefghijklmnopqrstuvwxyzABCD" not in out_secret
    assert "[redacted-secret]" in out_secret

    out_sas, _ = sanitize_issue_text(
        "https://acct.blob.core.windows.net/c/b?sv=2023-01-01&sig=AbCdEfGhIjKlMnOpQrStUvWxYz012345",
        is_public_target=True,
    )
    assert "AbCdEfGhIjKlMnOpQrStUvWxYz012345" not in out_sas
    assert "[redacted-secret]" in out_sas

    # Normal OTel trace ID and GitHub link must not trigger false positives.
    clean = (
        "**Trace ID:** af58ea288fe9139d441ced9646bc73b2\n"
        "**Source:** https://github.com/Azure/azure-sdk-for-net/blob/main/doc/dev/SDKBreakingChanges.md"
    )
    out_clean, redactions = sanitize_issue_text(clean, is_public_target=True)
    assert out_clean == clean
    assert redactions == []


def test_sanitize_issue_text_redacts_public_vs_private_targets():
    teams_url = (
        "https://teams.microsoft.com/l/message/19%3Aabc%40thread.skype/1793894"
        "?groupId=3e17dcb0-4257-4a30-b843-77f47f1d4121"
        "&tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47"
    )
    body = f"User alice@microsoft.com asked a question.\nSee {teams_url}"

    public_text, public_redactions = sanitize_issue_text(body, is_public_target=True)
    assert "alice@microsoft.com" not in public_text
    assert "teams.microsoft.com" not in public_text
    assert "n/a (internal Teams thread)" in public_text
    assert "EMAIL_ADDRESS" in public_redactions

    private_text, _ = sanitize_issue_text(body, is_public_target=False)
    assert "alice@microsoft.com" not in private_text
    assert "teams.microsoft.com" in private_text


def test_sanitize_log_text_and_mask_identifier():
    assert mask_identifier(None) == "none"
    masked = mask_identifier("alice@microsoft.com")
    assert masked.startswith("id:")
    assert "alice" not in masked

    log_out = sanitize_log_text(
        "alice@microsoft.com sent token ghp_1234567890abcdefghijklmnopqrstuvwxyzABCD "
        "from https://teams.microsoft.com/l/message/19%3Aabc/123"
    )
    assert "alice@microsoft.com" not in log_out
    assert "ghp_" not in log_out
    assert "teams.microsoft.com" not in log_out


def test_repo_visibility_helper():
    assert is_public_github_repo("Azure", "azure-sdk-pr") is False
    assert is_public_github_repo("Azure", "azure-sdk-for-net") is True


@pytest.mark.asyncio
async def test_middleware_sanitizes_public_issue_before_call():
    mw = IssuePublicationGuardMiddleware()
    called = False

    async def call_next():
        nonlocal called
        called = True

    write_ctx = _make_context(
        "issue_write",
        {
            "owner": "Azure",
            "repo": "azure-sdk-for-net",
            "title": "[Teams Chatbot]: Issue for alice@microsoft.com",
            "body": "### Conversation\nhttps://teams.microsoft.com/l/message/19%3Aabc/123",
        },
    )
    await mw.process(write_ctx, call_next)

    assert called is True
    assert "alice@microsoft.com" not in write_ctx.arguments["title"]
    assert "teams.microsoft.com" not in write_ctx.arguments["body"]
    assert "n/a (internal Teams thread)" in write_ctx.arguments["body"]


@pytest.mark.asyncio
async def test_middleware_keeps_teams_link_for_private_repo():
    mw = IssuePublicationGuardMiddleware()
    called = False

    async def call_next():
        nonlocal called
        called = True

    teams_link = "https://teams.microsoft.com/l/message/19%3Aabc/123"
    priv_ctx = _make_context(
        "issue_write",
        {
            "owner": "Azure",
            "repo": "azure-sdk-pr",
            "title": "Issue for alice@microsoft.com",
            "body": f"### Conversation\n{teams_link}",
        },
    )
    await mw.process(priv_ctx, call_next)
    assert called is True
    assert "alice@microsoft.com" not in priv_ctx.arguments["title"]
    assert teams_link in priv_ctx.arguments["body"]


@pytest.mark.asyncio
async def test_middleware_redacts_secrets_and_proceeds():
    mw = IssuePublicationGuardMiddleware()
    secret_ctx = _make_context(
        "issue_write",
        {
            "owner": "Azure",
            "repo": "azure-sdk-pr",
            "title": "Fix auth",
            "body": "token = 'ghp_1234567890abcdefghijklmnopqrstuvwxyzABCD'",
        },
    )
    called = False

    async def call_next():
        nonlocal called
        called = True

    await mw.process(secret_ctx, call_next)
    assert called is True
    assert "ghp_" not in secret_ctx.arguments["body"]
    assert "[redacted-secret]" in secret_ctx.arguments["body"]


def test_log_sanitization_filter_redacts_logger_records():
    import io
    import logging

    stream = io.StringIO()
    handler = logging.StreamHandler(stream)
    test_logger = logging.getLogger("test_central_log_sanitization")
    test_logger.handlers = [handler]
    test_logger.setLevel(logging.INFO)
    test_logger.propagate = False

    install_log_sanitization("test_central_log_sanitization")
    install_log_sanitization("test_central_log_sanitization")
    assert sum(isinstance(f, LogSanitizationFilter) for f in handler.filters) == 1

    test_logger.info(
        "User %s sent %s from %s",
        "alice@microsoft.com",
        "ghp_1234567890abcdefghijklmnopqrstuvwxyzABCD",
        "https://teams.microsoft.com/l/message/19%3Aabc/123",
    )
    output = stream.getvalue()
    assert "alice@microsoft.com" not in output
    assert "ghp_" not in output
    assert "teams.microsoft.com" not in output

