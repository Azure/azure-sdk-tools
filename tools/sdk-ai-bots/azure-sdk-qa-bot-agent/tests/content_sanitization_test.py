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

from middleware.tool_input_sanitization import ToolInputSanitizationMiddleware
from utils.content_sanitization import (
    LogSanitizationFilter,
    install_log_sanitization,
    is_public_github_repo,
    sanitize_text,
)


def _make_context(tool_name: str, arguments: dict, session=None):
    return SimpleNamespace(
        function=SimpleNamespace(name=tool_name),
        arguments=arguments,
        session=session,
        result=None,
    )


def test_sanitize_text_redacts_secrets_and_ignores_trace_ids():
    out_secret = sanitize_text(
        "token = 'ghp_1234567890abcdefghijklmnopqrstuvwxyzABCD'",
        is_public_target=True,
    )
    assert "ghp_" not in out_secret
    assert "1234567890abcdefghijklmnopqrstuvwxyzABCD" not in out_secret
    assert "<CREDENTIAL_OR_SECRET>" in out_secret

    out_sas = sanitize_text(
        "https://acct.blob.core.windows.net/c/b?sv=2023-01-01&sig=AbCdEfGhIjKlMnOpQrStUvWxYz012345",
        is_public_target=True,
    )
    assert "AbCdEfGhIjKlMnOpQrStUvWxYz012345" not in out_sas
    assert "<CREDENTIAL_OR_SECRET>" in out_sas

    pem_full = (
        "key:\n"
        "-----BEGIN RSA PRIVATE KEY-----\n"
        "MIIEpAIBAAKCAQEAsomeSecretKeyBodyLine1\n"
        "MIIEpAIBAAKCAQEAsomeSecretKeyBodyLine2\n"
        "-----END RSA PRIVATE KEY-----\n"
        "after"
    )
    out_pem = sanitize_text(pem_full, is_public_target=True)
    assert "someSecretKeyBodyLine1" not in out_pem
    assert "END RSA PRIVATE KEY" not in out_pem
    assert "<CREDENTIAL_OR_SECRET>" in out_pem
    assert "after" in out_pem

    pem_truncated = "-----BEGIN PRIVATE KEY-----\nMIIEvQIBADANBgkqhkiG9w0BAQEFAASCBKcw"
    out_trunc = sanitize_text(pem_truncated, is_public_target=True)
    assert "MIIEvQIBADANBgkqhkiG9w0BAQEFAASCBKcw" not in out_trunc
    assert "<CREDENTIAL_OR_SECRET>" in out_trunc

    # Normal OTel trace ID and GitHub link must not trigger false positives.
    clean = (
        "**Trace ID:** af58ea288fe9139d441ced9646bc73b2\n"
        "**Source:** https://github.com/Azure/azure-sdk-for-net/blob/main/doc/dev/SDKBreakingChanges.md"
    )
    assert sanitize_text(clean, is_public_target=True) == clean


def test_sanitize_text_redacts_public_vs_private_targets():
    teams_url = (
        "https://teams.microsoft.com/l/message/19%3Aabc%40thread.skype/1793894"
        "?groupId=3e17dcb0-4257-4a30-b843-77f47f1d4121"
        "&tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47"
    )
    body = f"User alice@microsoft.com asked a question.\nSee {teams_url}"

    public_text = sanitize_text(body, is_public_target=True)
    assert "alice@microsoft.com" not in public_text
    assert "<EMAIL_ADDRESS>" in public_text
    assert "teams.microsoft.com" not in public_text
    assert "n/a (internal Teams thread)" in public_text

    private_text = sanitize_text(body, is_public_target=False)
    assert "alice@microsoft.com" not in private_text
    assert "teams.microsoft.com" in private_text


def test_repo_visibility_helper():
    assert is_public_github_repo("Azure", "azure-sdk-pr") is False
    assert is_public_github_repo("Azure", "azure-sdk-for-net") is True


@pytest.mark.asyncio
async def test_middleware_sanitizes_public_issue_and_pr_before_call():
    mw = ToolInputSanitizationMiddleware()
    called = 0

    async def call_next():
        nonlocal called
        called += 1

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

    pr_ctx = _make_context(
        "create_pull_request",
        {
            "owner": "Azure",
            "repo": "azure-sdk-for-net",
            "title": "Fix doc for alice@microsoft.com",
            "body": "See https://teams.microsoft.com/l/message/19%3Aabc/123",
        },
    )
    await mw.process(pr_ctx, call_next)

    assert called == 2
    assert "alice@microsoft.com" not in write_ctx.arguments["title"]
    assert "teams.microsoft.com" not in write_ctx.arguments["body"]
    assert "n/a (internal Teams thread)" in write_ctx.arguments["body"]
    assert "alice@microsoft.com" not in pr_ctx.arguments["title"]
    assert "n/a (internal Teams thread)" in pr_ctx.arguments["body"]


@pytest.mark.asyncio
async def test_middleware_keeps_teams_link_for_private_repo():
    mw = ToolInputSanitizationMiddleware()
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
    mw = ToolInputSanitizationMiddleware()
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
    assert "<CREDENTIAL_OR_SECRET>" in secret_ctx.arguments["body"]


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

    stream.truncate(0)
    stream.seek(0)
    try:
        raise RuntimeError(
            "Failed for alice@microsoft.com with ghp_1234567890abcdefghijklmnopqrstuvwxyzABCD"
        )
    except RuntimeError:
        test_logger.exception("Unhandled error")
    exc_output = stream.getvalue()
    assert "RuntimeError" in exc_output
    assert "alice@microsoft.com" not in exc_output
    assert "ghp_" not in exc_output
    assert "<EMAIL_ADDRESS>" in exc_output
    assert "<CREDENTIAL_OR_SECRET>" in exc_output

