"""Ability tests for the conversation quality evaluator.

Each test builds a small, labelled conversation transcript representing one
core scenario of the confirmation-driven rubric (see
``prompts/conversation_evaluation.md``) and asserts the verdict the evaluator
returns. Like the intention-service tests, these exercise the real model, so
they measure the evaluator's judgement rather than deterministic plumbing.

Rubric under test:
    correct   — a human (poster or expert) confirms/agrees/acts on the bot.
    incorrect — a human corrects/contradicts or gives a materially different
                resolution (incl. missing a real defect the bot called clean).
    unknown   — no clear confirmation or correction signal.
"""

from __future__ import annotations

import sys
from datetime import datetime, timedelta, timezone
from pathlib import Path

import pytest
import pytest_asyncio

_PROJECT_ROOT = str(Path(__file__).resolve().parent.parent)
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

import config.app_config as app_config
from models.conversation import (
    BotAnswerVerdict,
    ConversationMessageItem,
    ConversationType,
    Role,
)
from services.conversation_service import ConversationService
from utils.azure_credential import close_credential

_CONVERSATION_ID = "conv-eval-test"
_PARTITION = f"channel:{_CONVERSATION_ID}"
_POSTER_ID = "poster-1"
_BOT_ID = "azure-sdk-qa-bot"
_BASE_TIME = datetime(2026, 7, 1, 12, 0, 0, tzinfo=timezone.utc)


@pytest_asyncio.fixture(scope="module")
async def service():
    await app_config.init()
    yield ConversationService()
    await close_credential()


def _poster(content: str, order: int) -> ConversationMessageItem:
    return _msg(Role.User, _POSTER_ID, "Asker", content, order)


def _bot(content: str, order: int) -> ConversationMessageItem:
    return _msg(Role.Assistant, _BOT_ID, "Azure SDK QA Bot", content, order)


def _expert(name: str, content: str, order: int) -> ConversationMessageItem:
    # An expert is any human other than the original poster: a User role with
    # a distinct sender_id.
    return _msg(Role.User, f"expert-{name}", name, content, order)


def _msg(
    role: Role, sender_id: str, name: str, content: str, order: int
) -> ConversationMessageItem:
    return ConversationMessageItem(
        id=f"msg-{order}",
        sender_role=role,
        sender_id=sender_id,
        sender_name=name,
        content=content,
        created_at=_BASE_TIME + timedelta(minutes=order),
        conversation_id=_CONVERSATION_ID,
        conversation_type=ConversationType.teams_channel,
        conversation_partition=_PARTITION,
    )


async def _verdict(
    service: ConversationService, messages: list[ConversationMessageItem]
) -> BotAnswerVerdict:
    result = await service.evaluate_conversation(messages)
    assert result is not None, "evaluator returned no result for a judgeable thread"
    return result.verdict


# ---------------------------------------------------------------------------
# correct — a human confirms / agrees / acts on the bot
# ---------------------------------------------------------------------------


@pytest.mark.asyncio(loop_scope="module")
async def test_correct_when_expert_confirms(service: ConversationService) -> None:
    """Expert agreeing with the bot's answer -> correct."""
    messages = [
        _poster(
            "How do I add a new API version to my TypeSpec spec so the old "
            "version stays supported?",
            0,
        ),
        _bot(
            "Add a new value to your `@versioned` enum and mark the new members "
            "with `@added(Versions.v2026_06_01)`. The previous version stays "
            "generated because its members remain in the enum.",
            1,
        ),
        _expert(
            "Mark Cowlishaw",
            "Yes, that's exactly right — use @added on the new members and keep "
            "the old version in the enum. Nothing else needed.",
            2,
        ),
    ]
    assert await _verdict(service, messages) == BotAnswerVerdict.Correct


@pytest.mark.asyncio(loop_scope="module")
async def test_correct_when_poster_confirms(service: ConversationService) -> None:
    """Original poster confirming the fix worked -> correct."""
    messages = [
        _poster(
            "My Python PR CI fails with 'package not found on feed' for a "
            "dependency I didn't change. What's wrong?",
            0,
        ),
        _bot(
            "That dependency version is newly released and still quarantined on "
            "the internal feed. Pin the previously available version in a "
            "constraints file, or wait for the quarantine window to lapse, then "
            "re-run CI.",
            1,
        ),
        _poster("Pinning it in the constraints file fixed it, thanks!", 2),
    ]
    assert await _verdict(service, messages) == BotAnswerVerdict.Correct


# ---------------------------------------------------------------------------
# incorrect — a human corrects / gives a materially different resolution
# ---------------------------------------------------------------------------


@pytest.mark.asyncio(loop_scope="module")
async def test_incorrect_when_expert_corrects(service: ConversationService) -> None:
    """Expert supplying a materially different resolution -> incorrect."""
    messages = [
        _poster(
            "My spec PR is blocked and I can't merge. How do I unblock it?",
            0,
        ),
        _bot(
            "The blocker is the shared version-resolution tooling. Update the "
            "shared tooling version in your pipeline config and re-run to unblock "
            "the merge.",
            1,
        ),
        _expert(
            "Mike Harder",
            "That's not it. The real problem is the AutoRest install step in the "
            "validation script. We removed that Swagger/AutoRest validation step "
            "entirely and the PR merged — the shared tooling wasn't involved.",
            2,
        ),
    ]
    assert await _verdict(service, messages) == BotAnswerVerdict.Incorrect


@pytest.mark.asyncio(loop_scope="module")
async def test_incorrect_when_bot_misses_real_blocker(
    service: ConversationService,
) -> None:
    """Bot calls the PR effectively clean; expert flags a real blocker -> incorrect."""
    messages = [
        _poster("Can you review PR #29185? Is anything blocking the merge?", 0),
        _bot(
            "As of today all automated merging requirements are met on PR #29185 "
            "— this looks resolved already and there's no remaining blocker.",
            1,
        ),
        _expert(
            "Mike Harder",
            "That's wrong — your PR is still blocked because Swagger PrettierCheck "
            "is failing. You need to fix the formatting before it can merge.",
            2,
        ),
    ]
    assert await _verdict(service, messages) == BotAnswerVerdict.Incorrect


# ---------------------------------------------------------------------------
# unknown — no clear confirmation or correction signal
# ---------------------------------------------------------------------------


@pytest.mark.asyncio(loop_scope="module")
async def test_unknown_when_no_human_reply(service: ConversationService) -> None:
    """Bot answers but no poster/expert ever confirms or corrects -> unknown."""
    messages = [
        _poster(
            "Who owns the release pipeline for the azure-mgmt-keyvault package?",
            0,
        ),
        _bot(
            "That package is released through the internal Python release "
            "pipeline; re-run it with the BuildTargetingString set to "
            "azure-mgmt-keyvault to produce a fresh release build.",
            1,
        ),
    ]
    assert await _verdict(service, messages) == BotAnswerVerdict.Unknown


@pytest.mark.asyncio(loop_scope="module")
async def test_unknown_when_expert_only_redirects(
    service: ConversationService,
) -> None:
    """Expert only defers/redirects without endorsing or correcting -> unknown."""
    messages = [
        _poster(
            "Seeking help to convert the DBforPostgreSQL spec to folder "
            "structure v2. Any downtime risk?",
            0,
        ),
        _bot(
            "This is a repo layout migration, not a runtime change, so it should "
            "not cause service downtime. Rebase after the pending infra PR merges "
            "and convert to the v2 directory layout.",
            1,
        ),
        _expert(
            "Qiaoqiao Zhang",
            "Let's track this in the API Spec Review channel — the folder-structure "
            "owners over there can pick it up with you. Moving the thread.",
            2,
        ),
    ]
    assert await _verdict(service, messages) == BotAnswerVerdict.Unknown


@pytest.mark.asyncio(loop_scope="module")
async def test_unknown_when_bot_only_errors(service: ConversationService) -> None:
    """Bot emits only a system/generation error, so there's nothing to judge -> unknown."""
    messages = [
        _poster("How do I regenerate the Go SDK after updating my TypeSpec?", 0),
        _bot("Sorry, something went wrong while generating a response. Please retry.", 1),
    ]
    assert await _verdict(service, messages) == BotAnswerVerdict.Unknown
