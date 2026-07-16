"""Unit tests for the intention classification service."""

from __future__ import annotations

import sys
from datetime import datetime, timezone
from pathlib import Path

import pytest
import pytest_asyncio

_PROJECT_ROOT = str(Path(__file__).resolve().parent.parent)
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

import config.app_config as app_config
from models.chat import Message
from models.conversation import (
    ConversationMessageItem,
    ConversationType,
    Role,
)
from models.intention import IntentionRequest
from services.intention_service import IntentionService


@pytest_asyncio.fixture(scope="module")
async def service() -> IntentionService:
    await app_config.init()
    return IntentionService()


@pytest.mark.asyncio(loop_scope="module")
async def test_technical_question_should_respond(service: IntentionService) -> None:
    """Standard technical question should be classified as should_respond=true."""
    req = IntentionRequest(
        message=Message(
            role="user",
            content="How do I generate a Python SDK from my TypeSpec definition?",
        ),
    )
    resp = await service.classify(req)
    assert resp.should_respond is True


@pytest.mark.asyncio(loop_scope="module")
async def test_casual_message_should_not_respond(service: IntentionService) -> None:
    """Casual greeting should be classified as should_respond=false."""
    req = IntentionRequest(
        message=Message(
            role="user",
            content="Thanks everyone, happy Friday! Have a great weekend.",
        ),
    )
    resp = await service.classify(req)
    assert resp.should_respond is False


@pytest.mark.asyncio(loop_scope="module")
async def test_pr_review_request_should_respond(service: IntentionService) -> None:
    """PR review request with breaking change labels should be classified as should_respond=true."""
    req = IntentionRequest(
        message=Message(
            role="user",
            content=(
                "Requesting review for PR\n\n"
                "Hii API Spec Review\n"
                "We are syncing all recent stable changes from the ARM private (Azure-Rest-API-specs-pr) "
                "repository to the public (Azure-Rest_API-Specs) repository, so we require breaking change "
                "approvals for this public repo PR.\n"
                "These are the breaking change labels currently present for which approval is required:\n"
                "- BreakingChange-JavaScript-Sdk-Suppression\n"
                "- BreakingChange-Python-Sdk-Suppression\n"
                "- BreakingChangeReviewRequired\n\n"
                "Thanks in Advance."
            ),
        ),
    )
    resp = await service.classify(req)
    assert resp.should_respond is True


@pytest.mark.asyncio(loop_scope="module")
async def test_human_approval_request_should_not_respond(
    service: IntentionService, monkeypatch: pytest.MonkeyPatch
) -> None:
    """When the user explicitly asks a human to approve/confirm the bot's prior reply, the bot should not respond.

    Stubs conversation history so the LLM sees a prior bot/assistant message that
    the current user message is escalating to a human.
    """
    user_id = "user-123"
    conversation_id = "conv-abc"
    conversation_type = ConversationType.teams_channel

    prior_user_msg = ConversationMessageItem(
        id="msg-1",
        sender_role=Role.User,
        sender_id=user_id,
        sender_name="Asker",
        content="How can I get a private signed .NET SDK from a PR against the public repo main?",
        created_at=datetime.now(timezone.utc),
        conversation_id=conversation_id,
        conversation_type=conversation_type,
        conversation_partition=f"channel:{conversation_id}",
    )
    prior_bot_reply = ConversationMessageItem(
        id="msg-2",
        sender_role=Role.Assistant,
        sender_id="bot",
        sender_name="Azure SDK QA Bot",
        content=(
            "Based on the docs, private signed .NET SDKs are produced by the internal "
            "release pipeline rather than from public PRs. You typically need to use the "
            "internal build outputs for E2E validation before public rollout."
        ),
        created_at=datetime.now(timezone.utc),
        conversation_id=conversation_id,
        conversation_type=conversation_type,
        conversation_partition=f"channel:{conversation_id}",
    )

    async def fake_has_expert_reply(*_args, **_kwargs):
        return False

    async def fake_get_messages_by_conversation(*_args, **_kwargs):
        return [prior_user_msg, prior_bot_reply]

    monkeypatch.setattr(
        service._conversation_service,
        "has_expert_reply",
        fake_has_expert_reply,
    )
    monkeypatch.setattr(
        service._conversation_service,
        "get_messages_by_conversation",
        fake_get_messages_by_conversation,
    )

    req = IntentionRequest(
        message=Message(
            role=Role.User,
            user_id=user_id,
            content=(
                "@Azure SDK Onboarding and E2E SDK can some human approve the above AI-generated response?\n\n"
                "And can you please confirm whether we can get private Signed .NET SDK from PR against "
                "Public Repo Main? We use private SDK for daily E2E test workflow before we rollout to Public."
            ),
        ),
        conversation_id=conversation_id,
        conversation_type=conversation_type,
    )
    resp = await service.classify(req)
    assert resp.should_respond is False


@pytest.mark.asyncio(loop_scope="module")
async def test_pr_help_review_should_respond(service: IntentionService) -> None:
    """PR help review request should be classified as should_respond=true."""
    req = IntentionRequest(
        message=Message(
            role="user",
            content=(
                "PR #38482 Review Request\n\n"
                "Hey team, could you please help review this PR "
                "[AutoPR @azure-arm-containerservice]-generated-from-SDK Generation - JS-6274121? Thanks"
            ),
        ),
    )
    resp = await service.classify(req)
    assert resp.should_respond is True


@pytest.mark.asyncio(loop_scope="module")
async def test_domain_question_with_user_mention_should_respond(
    service: IntentionService,
) -> None:
    """A domain question that @-mentions a teammate should still be answered.

    Covers the bad case from issue Azure/azure-sdk-pr#2643: previously the
    Logic App skipped messages that mentioned anyone, so technical questions
    addressed to "@SomeoneElse" never reached the bot.
    """
    req = IntentionRequest(
        message=Message(
            role="user",
            content=(
                "<at>John Doe</at> any idea why my TypeSpec build keeps failing with "
                "`unable to resolve module @azure-tools/typespec-azure-core` after I "
                "bumped the emitter version? Anything obvious I should check first?"
            ),
        ),
    )
    resp = await service.classify(req)
    assert resp.should_respond is True


@pytest.mark.asyncio(loop_scope="module")
async def test_personal_ask_with_user_mention_should_not_respond(
    service: IntentionService,
) -> None:
    """A message that is a private/personal ask to a specific person should not be answered."""
    req = IntentionRequest(
        message=Message(
            role="user",
            content=(
                "<at>Alice</at> can you take a look at my PR when you have a moment? "
                "No rush, just whenever you're free this week. Thanks!"
            ),
        ),
    )
    resp = await service.classify(req)
    assert resp.should_respond is False


@pytest.mark.asyncio(loop_scope="module")
async def test_bot_no_reply_nudge_should_not_respond(
    service: IntentionService, monkeypatch: pytest.MonkeyPatch
) -> None:
    """A nudge to a human that the bot did not reply should not trigger a bot response.

    Real case: the bot should have answered the prior technical question but failed
    due to a system error. A teammate then @-mentions a maintainer pointing out the
    bot did not reply. That message is commentary about the bot's behavior, not a
    technical question, so the bot must not answer it.
    """
    user_id = "user-456"
    conversation_id = "conv-xyz"
    conversation_type = ConversationType.teams_channel

    prior_user_msg = ConversationMessageItem(
        id="msg-1",
        sender_role=Role.User,
        sender_id=user_id,
        sender_name="Bob",
        content="Then who will review the suppression and add the label?",
        created_at=datetime.now(timezone.utc),
        conversation_id=conversation_id,
        conversation_type=conversation_type,
        conversation_partition=f"channel:{conversation_id}",
    )

    async def fake_has_expert_reply(*_args, **_kwargs):
        return False

    async def fake_get_messages_by_conversation(*_args, **_kwargs):
        return [prior_user_msg]

    monkeypatch.setattr(
        service._conversation_service,
        "has_expert_reply",
        fake_has_expert_reply,
    )
    monkeypatch.setattr(
        service._conversation_service,
        "get_messages_by_conversation",
        fake_get_messages_by_conversation,
    )

    req = IntentionRequest(
        message=Message(
            role=Role.User,
            user_id=user_id,
            content="<at>Carol</at>, no reply for the above question....",
        ),
        conversation_id=conversation_id,
        conversation_type=conversation_type,
    )
    resp = await service.classify(req)
    assert resp.should_respond is False


@pytest.mark.asyncio(loop_scope="module")
async def test_bot_broken_complaint_should_not_respond(
    service: IntentionService,
) -> None:
    """Commentary that the bot seems broken is about the bot, not a technical ask."""
    req = IntentionRequest(
        message=Message(
            role="user",
            content="<at>Carol</at> why didn't the bot answer this one? Is it down?",
        ),
    )
    resp = await service.classify(req)
    assert resp.should_respond is False


@pytest.mark.asyncio(loop_scope="module")
async def test_troubleshooting_help_should_respond(service: IntentionService) -> None:
    """A troubleshooting/help request in the bot's domain should be answered."""
    req = IntentionRequest(
        message=Message(
            role="user",
            content=(
                "My TypeSpec compile fails with `error: cannot find emitter "
                "@azure-tools/typespec-autorest`. How do I fix this?"
            ),
        ),
    )
    resp = await service.classify(req)
    assert resp.should_respond is True


@pytest.mark.asyncio(loop_scope="module")
async def test_status_update_should_not_respond(service: IntentionService) -> None:
    """A status/FYI announcement that does not seek help should not be answered."""
    req = IntentionRequest(
        message=Message(
            role="user",
            content=(
                "FYI everyone: the release pipeline has been upgraded to the new "
                "build agents. No action needed on your side, just a heads up."
            ),
        ),
    )
    resp = await service.classify(req)
    assert resp.should_respond is False


@pytest.mark.asyncio(loop_scope="module")
async def test_rhetorical_comment_should_not_respond(service: IntentionService) -> None:
    """A rhetorical/thinking-aloud remark that does not seek an answer should not be answered."""
    req = IntentionRequest(
        message=Message(
            role="user",
            content="Ugh, why is naming things always the hardest part of API design... anyway.",
        ),
    )
    resp = await service.classify(req)
    assert resp.should_respond is False


@pytest.mark.asyncio(loop_scope="module")
async def test_followup_to_bot_reply_should_respond(
    service: IntentionService, monkeypatch: pytest.MonkeyPatch
) -> None:
    """A non-question follow-up that continues the bot's technical thread should be answered."""
    user_id = "user-789"
    conversation_id = "conv-follow"
    conversation_type = ConversationType.teams_channel

    prior_user_msg = ConversationMessageItem(
        id="msg-1",
        sender_role=Role.User,
        sender_id=user_id,
        sender_name="Dave",
        content="How do I enable the Python SDK emitter in my tspconfig.yaml?",
        created_at=datetime.now(timezone.utc),
        conversation_id=conversation_id,
        conversation_type=conversation_type,
        conversation_partition=f"channel:{conversation_id}",
    )
    prior_bot_reply = ConversationMessageItem(
        id="msg-2",
        sender_role=Role.Assistant,
        sender_id="bot",
        sender_name="Azure SDK QA Bot",
        content=(
            "Add `@azure-tools/typespec-python` under the `emit` section of your "
            "tspconfig.yaml and configure its options under `options`."
        ),
        created_at=datetime.now(timezone.utc),
        conversation_id=conversation_id,
        conversation_type=conversation_type,
        conversation_partition=f"channel:{conversation_id}",
    )

    async def fake_has_expert_reply(*_args, **_kwargs):
        return False

    async def fake_get_messages_by_conversation(*_args, **_kwargs):
        return [prior_user_msg, prior_bot_reply]

    monkeypatch.setattr(
        service._conversation_service, "has_expert_reply", fake_has_expert_reply
    )
    monkeypatch.setattr(
        service._conversation_service,
        "get_messages_by_conversation",
        fake_get_messages_by_conversation,
    )

    req = IntentionRequest(
        message=Message(
            role=Role.User,
            user_id=user_id,
            content="Still seeing no Python output after adding that emitter.",
        ),
        conversation_id=conversation_id,
        conversation_type=conversation_type,
    )
    resp = await service.classify(req)
    assert resp.should_respond is True


@pytest.mark.asyncio(loop_scope="module")
async def test_pushback_on_bot_answer_should_respond(
    service: IntentionService, monkeypatch: pytest.MonkeyPatch
) -> None:
    """A user pushing back on / correcting the bot's earlier answer should be answered."""
    user_id = "user-987"
    conversation_id = "conv-push"
    conversation_type = ConversationType.teams_channel

    prior_user_msg = ConversationMessageItem(
        id="msg-1",
        sender_role=Role.User,
        sender_id=user_id,
        sender_name="Eve",
        content="Which branch should I target for a breaking change suppression PR?",
        created_at=datetime.now(timezone.utc),
        conversation_id=conversation_id,
        conversation_type=conversation_type,
        conversation_partition=f"channel:{conversation_id}",
    )
    prior_bot_reply = ConversationMessageItem(
        id="msg-2",
        sender_role=Role.Assistant,
        sender_id="bot",
        sender_name="Azure SDK QA Bot",
        content="You should target the `main` branch of the public specs repo.",
        created_at=datetime.now(timezone.utc),
        conversation_id=conversation_id,
        conversation_type=conversation_type,
        conversation_partition=f"channel:{conversation_id}",
    )

    async def fake_has_expert_reply(*_args, **_kwargs):
        return False

    async def fake_get_messages_by_conversation(*_args, **_kwargs):
        return [prior_user_msg, prior_bot_reply]

    monkeypatch.setattr(
        service._conversation_service, "has_expert_reply", fake_has_expert_reply
    )
    monkeypatch.setattr(
        service._conversation_service,
        "get_messages_by_conversation",
        fake_get_messages_by_conversation,
    )

    req = IntentionRequest(
        message=Message(
            role=Role.User,
            user_id=user_id,
            content=(
                "That doesn't sound right — my PR is against the RPSaaSMaster branch, "
                "not main. Does the same guidance still apply?"
            ),
        ),
        conversation_id=conversation_id,
        conversation_type=conversation_type,
    )
    resp = await service.classify(req)
    assert resp.should_respond is True


@pytest.mark.asyncio(loop_scope="module")
async def test_thank_you_closure_after_bot_reply_should_not_respond(
    service: IntentionService, monkeypatch: pytest.MonkeyPatch
) -> None:
    """A thank-you that closes out the bot's thread should not trigger another reply."""
    user_id = "user-111"
    conversation_id = "conv-close"
    conversation_type = ConversationType.teams_channel

    prior_bot_reply = ConversationMessageItem(
        id="msg-1",
        sender_role=Role.Assistant,
        sender_id="bot",
        sender_name="Azure SDK QA Bot",
        content="Run `tsp compile .` from the spec folder to regenerate the SDK.",
        created_at=datetime.now(timezone.utc),
        conversation_id=conversation_id,
        conversation_type=conversation_type,
        conversation_partition=f"channel:{conversation_id}",
    )

    async def fake_has_expert_reply(*_args, **_kwargs):
        return False

    async def fake_get_messages_by_conversation(*_args, **_kwargs):
        return [prior_bot_reply]

    monkeypatch.setattr(
        service._conversation_service, "has_expert_reply", fake_has_expert_reply
    )
    monkeypatch.setattr(
        service._conversation_service,
        "get_messages_by_conversation",
        fake_get_messages_by_conversation,
    )

    req = IntentionRequest(
        message=Message(
            role=Role.User,
            user_id=user_id,
            content="That worked, thanks a lot!",
        ),
        conversation_id=conversation_id,
        conversation_type=conversation_type,
    )
    resp = await service.classify(req)
    assert resp.should_respond is False


@pytest.mark.asyncio(loop_scope="module")
async def test_pre_deployment_thread_reply_should_not_respond(
    service: IntentionService, monkeypatch: pytest.MonkeyPatch
) -> None:
    """A reply in a thread that started before deployment should not be answered.

    Covers issue Azure/azure-sdk-pr#2676: the root message (id 1000) predates the
    agent so it was never saved. Only the reply (id 2000) is in history. Since the
    reply is not the thread root, the bot must not treat it as a new question.
    """
    conversation_id = "19:channel@thread.skype;messageid=1000"
    conversation_type = ConversationType.teams_channel

    reply = ConversationMessageItem(
        id="2000",
        sender_role=Role.User,
        sender_id="user-222",
        sender_name="Asker",
        content="How do I generate a Python SDK from my TypeSpec definition?",
        created_at=datetime.now(timezone.utc),
        conversation_id=conversation_id,
        conversation_type=conversation_type,
        conversation_partition=f"channel:{conversation_id}",
    )

    async def fake_get_messages_by_conversation(*_args, **_kwargs):
        return [reply]

    monkeypatch.setattr(
        service._conversation_service,
        "get_messages_by_conversation",
        fake_get_messages_by_conversation,
    )

    req = IntentionRequest(
        message=Message(
            id="2000",
            role=Role.User,
            user_id="user-222",
            content="How do I generate a Python SDK from my TypeSpec definition?",
        ),
        conversation_id=conversation_id,
        conversation_type=conversation_type,
    )
    resp = await service.classify(req)
    assert resp.should_respond is False
    assert resp.reason == "no_history_and_not_root_message"


@pytest.mark.asyncio(loop_scope="module")
async def test_human_assistance_plea_after_bot_reply_should_not_respond(
    service: IntentionService, monkeypatch: pytest.MonkeyPatch
) -> None:
    """A generic plea for a human to assist after the bot already answered should not trigger another reply.

    Real case: the bot answered the technical question, then the asker posted a
    generic "Can someone please assist on this request?" to escalate to a human.
    That is a thread-bump directed at people, not a new question for the bot.
    """
    user_id = "user-333"
    conversation_id = "conv-plea"
    conversation_type = ConversationType.teams_channel

    prior_user_msg = ConversationMessageItem(
        id="msg-1",
        sender_role=Role.User,
        sender_id=user_id,
        sender_name="Frank",
        content="Why are my PRs stuck on the license/cla check and how do I unblock them?",
        created_at=datetime.now(timezone.utc),
        conversation_id=conversation_id,
        conversation_type=conversation_type,
        conversation_partition=f"channel:{conversation_id}",
    )
    prior_bot_reply = ConversationMessageItem(
        id="msg-2",
        sender_role=Role.Assistant,
        sender_id="bot",
        sender_name="Azure SDK QA Bot",
        content=(
            "The license/cla check blocks PRs until the CLA is signed. Ask the PR "
            "author to sign the CLA, then re-run the check to unblock the PR."
        ),
        created_at=datetime.now(timezone.utc),
        conversation_id=conversation_id,
        conversation_type=conversation_type,
        conversation_partition=f"channel:{conversation_id}",
    )

    async def fake_has_expert_reply(*_args, **_kwargs):
        return False

    async def fake_get_messages_by_conversation(*_args, **_kwargs):
        return [prior_user_msg, prior_bot_reply]

    monkeypatch.setattr(
        service._conversation_service, "has_expert_reply", fake_has_expert_reply
    )
    monkeypatch.setattr(
        service._conversation_service,
        "get_messages_by_conversation",
        fake_get_messages_by_conversation,
    )

    req = IntentionRequest(
        message=Message(
            role=Role.User,
            user_id=user_id,
            content="Can someone please assist on this request? Thanks!",
        ),
        conversation_id=conversation_id,
        conversation_type=conversation_type,
    )
    resp = await service.classify(req)
    assert resp.should_respond is False


@pytest.mark.asyncio(loop_scope="module")
async def test_cc_routing_addendum_should_not_respond(
    service: IntentionService, monkeypatch: pytest.MonkeyPatch
) -> None:
    """A pure cc/fyi routing addendum that only loops in a teammate should not be answered.

    Real case: the bot already answered the technical question, then the asker
    posts a "cc @Alice fyi" addendum to loop in a teammate. That is a routing
    ping directed at people, not a new question for the bot.
    """
    user_id = "user-444"
    conversation_id = "conv-cc"
    conversation_type = ConversationType.teams_channel

    prior_user_msg = ConversationMessageItem(
        id="msg-1",
        sender_role=Role.User,
        sender_id=user_id,
        sender_name="Grace",
        content="How do I add a suppression for a breaking change in my spec PR?",
        created_at=datetime.now(timezone.utc),
        conversation_id=conversation_id,
        conversation_type=conversation_type,
        conversation_partition=f"channel:{conversation_id}",
    )
    prior_bot_reply = ConversationMessageItem(
        id="msg-2",
        sender_role=Role.Assistant,
        sender_id="bot",
        sender_name="Azure SDK QA Bot",
        content=(
            "Add a suppression entry under the `suppressions` section of your PR and "
            "have an approver apply the breaking-change label."
        ),
        created_at=datetime.now(timezone.utc),
        conversation_id=conversation_id,
        conversation_type=conversation_type,
        conversation_partition=f"channel:{conversation_id}",
    )

    async def fake_has_expert_reply(*_args, **_kwargs):
        return False

    async def fake_get_messages_by_conversation(*_args, **_kwargs):
        return [prior_user_msg, prior_bot_reply]

    monkeypatch.setattr(
        service._conversation_service, "has_expert_reply", fake_has_expert_reply
    )
    monkeypatch.setattr(
        service._conversation_service,
        "get_messages_by_conversation",
        fake_get_messages_by_conversation,
    )

    req = IntentionRequest(
        message=Message(
            role=Role.User,
            user_id=user_id,
            content="cc <at>Alice</at> /cc <at>SDK Team</at> fyi",
        ),
        conversation_id=conversation_id,
        conversation_type=conversation_type,
    )
    resp = await service.classify(req)
    assert resp.should_respond is False


@pytest.mark.asyncio(loop_scope="module")
async def test_assistance_plea_after_cc_ping_should_not_respond(
    service: IntentionService, monkeypatch: pytest.MonkeyPatch
) -> None:
    """A human-assistance plea that trails a cc routing ping should not be answered.

    Real case (screenshot): the bot answered the PR merge-readiness question, the
    asker then posted a ``cc @<account>`` ping followed by "Can someone please
    assist on this request? Thanks!". The plea is a hand-off to a human, not a
    follow-up for the bot, even though it trails the cc ping and the technical
    thread. The bot must stay silent instead of offering to draft a nudge.
    """
    user_id = "user-555"
    conversation_id = "conv-plea-cc"
    conversation_type = ConversationType.teams_channel

    prior_user_msg = ConversationMessageItem(
        id="msg-1",
        sender_role=Role.User,
        sender_id=user_id,
        sender_name="Heidi",
        content="Is this PR ready to merge, or is something still blocking it?",
        created_at=datetime.now(timezone.utc),
        conversation_id=conversation_id,
        conversation_type=conversation_type,
        conversation_partition=f"channel:{conversation_id}",
    )
    prior_bot_reply = ConversationMessageItem(
        id="msg-2",
        sender_role=Role.Assistant,
        sender_id="bot",
        sender_name="Azure SDK QA Bot",
        content=(
            "This PR is not ready to merge yet: checks are green, but it is still "
            "missing human approval (branch protection / CODEOWNERS). Get an "
            "approving review from a requested reviewer to unblock the merge.\n\n"
            "Not resolved? Please re-post in the Language - Python channel."
        ),
        created_at=datetime.now(timezone.utc),
        conversation_id=conversation_id,
        conversation_type=conversation_type,
        conversation_partition=f"channel:{conversation_id}",
    )
    cc_ping_msg = ConversationMessageItem(
        id="msg-3",
        sender_role=Role.User,
        sender_id=user_id,
        sender_name="Heidi",
        content="cc <at>non-people-account-for-chatbot</at>",
        created_at=datetime.now(timezone.utc),
        conversation_id=conversation_id,
        conversation_type=conversation_type,
        conversation_partition=f"channel:{conversation_id}",
    )

    async def fake_has_expert_reply(*_args, **_kwargs):
        return False

    async def fake_get_messages_by_conversation(*_args, **_kwargs):
        return [prior_user_msg, prior_bot_reply, cc_ping_msg]

    monkeypatch.setattr(
        service._conversation_service, "has_expert_reply", fake_has_expert_reply
    )
    monkeypatch.setattr(
        service._conversation_service,
        "get_messages_by_conversation",
        fake_get_messages_by_conversation,
    )

    req = IntentionRequest(
        message=Message(
            role=Role.User,
            user_id=user_id,
            content="Can someone please assist on this request? Thanks!",
        ),
        conversation_id=conversation_id,
        conversation_type=conversation_type,
    )
    resp = await service.classify(req)
    assert resp.should_respond is False


