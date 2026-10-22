"""Notification eligibility is independent of whether an answer is returned."""

from datetime import datetime, timezone

import pytest

from models.bot_config import BotSettings
from models.chat import AssessedAnswer
from models.conversation import ConversationMessageItem
from services.chat_service import ChatService


def assessment(level="high", *, needs_expert_help=False, unresolved_needs=()):
    return AssessedAnswer(
        answer="Useful guidance",
        confidence={
            "level": level,
            "summary": "Confidence in the provided guidance",
            "unresolved_needs": list(unresolved_needs),
            "needs_expert_help": needs_expert_help,
        },
    )


def message(id="root", sender="author", content="Question", role="user"):
    return ConversationMessageItem(
        id=id, sender_role=role, sender_id=sender, sender_name=sender,
        content=content, created_at=datetime(2026, 9, 18, tzinfo=timezone.utc),
        conversation_id="channel;messageid=root", conversation_type="teams_channel",
        conversation_partition="teams_channel:channel;messageid=root",
    )


@pytest.mark.parametrize("level,threshold,below_threshold", [
    ("high", "high", False),
    ("medium", "high", True),
    ("low", "high", True),
    ("high", "medium", False),
    ("medium", "medium", False),
    ("low", "medium", True),
    ("high", "low", False),
    ("medium", "low", False),
    ("low", "low", False),
])
def test_notification_threshold_boundaries(level, threshold, below_threshold):
    settings = BotSettings(
        allow_notify_experts=True,
        expert_help_threshold=threshold,
        experts=[{"id": "expert", "name": "Expert"}],
    )
    assert ChatService._should_notify_experts(assessment(level).confidence, settings) is below_threshold


@pytest.mark.parametrize("level", ["high", "medium", "low"])
def test_explicit_help_qualifies_even_at_lowest_threshold(level):
    settings = BotSettings(
        allow_notify_experts=True, expert_help_threshold="low",
        experts=[{"id": "expert", "name": "Expert"}],
    )
    assert ChatService._should_notify_experts(
        assessment(level, needs_expert_help=True).confidence, settings
    )


@pytest.mark.parametrize("level,notify", [("high", False), ("low", True)])
def test_unresolved_needs_do_not_change_notification_eligibility(level, notify):
    settings = BotSettings(
        allow_notify_experts=True, experts=[{"id": "expert", "name": "Expert"}]
    )
    assert ChatService._should_notify_experts(
        assessment(level, unresolved_needs=["Missing user details"]).confidence, settings
    ) is notify


@pytest.mark.parametrize("settings", [
    {"show_confidence_label": True, "allow_replies_after_humans": True,
     "experts": [{"id": "expert", "name": "Expert"}]},
    {"allow_notify_experts": True, "experts": []},
])
def test_notification_requires_both_optin_and_roster(settings):
    assert not ChatService._should_notify_experts(
        assessment("low", needs_expert_help=True).confidence,
        BotSettings.model_validate(settings),
    )


@pytest.mark.parametrize("field,value", [
    ("level", "certain"),
    ("scope", "full"),
    ("needs_expert_help", "false"),
    ("needs_expert_help", 0),
])
def test_confidence_schema_rejects_invalid_values_and_removed_fields(field, value):
    data = assessment().model_dump()
    data["confidence"][field] = value
    with pytest.raises(ValueError):
        AssessedAnswer.model_validate(data)


def test_assessed_answer_only_contains_answer_and_confidence():
    assert set(AssessedAnswer.model_fields) == {"answer", "confidence"}
    assert set(assessment().model_dump()) == {"answer", "confidence"}
    assert set(assessment().confidence.model_dump()) == {
        "level", "summary", "unresolved_needs", "needs_expert_help"
    }
