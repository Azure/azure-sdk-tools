"""Live tests for the intention classification service."""

from __future__ import annotations

import sys
from pathlib import Path

import pytest
import pytest_asyncio

_PROJECT_ROOT = str(Path(__file__).resolve().parent.parent)
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

import config.app_config as app_config
from models.chat import Message
from models.intention import IntentionRequest
from services.intention_service import IntentionService


@pytest_asyncio.fixture(scope="module")
async def service() -> IntentionService:
    await app_config.init()
    return IntentionService()


@pytest.mark.asyncio
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


@pytest.mark.asyncio
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


@pytest.mark.asyncio
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


@pytest.mark.asyncio
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
