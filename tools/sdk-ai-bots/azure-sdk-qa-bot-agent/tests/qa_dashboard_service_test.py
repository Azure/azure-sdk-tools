"""Tests for the QA record dashboard query and routes."""

from __future__ import annotations

from datetime import datetime, timezone
import base64
import json
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import AsyncMock, patch

import httpx
import pytest

from models.conversation import (
    ConversationMessageItem,
    ConversationType,
    Role,
)
from models.qa_dashboard import (
    DashboardChannel,
    QADashboardDetail,
    QADashboardRecord,
    FeedbackStatusFilter,
    QARecordPage,
)
from models.qa_record import FeedbackState, FeedbackStatus, QARecord, QAStatus
from services.qa_dashboard_service import QADashboardService
from utils.dashboard_identity import get_dashboard_identity


class _FakeContainer:
    def __init__(self, document: dict, *, count: int = 1) -> None:
        self.document = document
        self.count = count
        self.calls: list[dict] = []

    async def query_items(self, **kwargs):
        self.calls.append(kwargs)
        query = kwargs["query"]
        if "COUNT(1)" in query:
            yield self.count
        elif "DISTINCT VALUE c.channel_id" in query:
            yield "channel-b"
            yield "channel-a"
        else:
            yield self.document


def _record() -> QARecord:
    now = datetime(2026, 8, 7, tzinfo=timezone.utc)
    return QARecord(
        id="teams_channel:conversation-1",
        tenant_id="tenant-a",
        conversation_id="conversation-1",
        conversation_type=ConversationType.teams_channel,
        channel_id="channel-a",
        qa_status=QAStatus.failed,
        conversation_created_at=now,
        feedback=FeedbackState(
            status=FeedbackStatus.pending_validation,
            issue_url="https://github.com/Azure/azure-sdk-pr/issues/123",
            created_at=now,
            updated_at=now,
        ),
        first_seen_at=now,
        created_at=now,
        updated_at=now,
    )


def _dashboard_record() -> QADashboardRecord:
    return QADashboardRecord(
        **_record().model_dump(),
        conversation_title="How do I fix this?",
        channel_name="Channel A",
    )


def _message(
    *,
    message_id: str,
    role: Role,
    sender_name: str,
    content: str,
    minute: int,
) -> ConversationMessageItem:
    return ConversationMessageItem(
        id=message_id,
        tenant_id="tenant-a",
        sender_role=role,
        sender_id=sender_name.lower(),
        sender_name=sender_name,
        content=content,
        created_at=datetime(2026, 8, 7, 10, minute, tzinfo=timezone.utc),
        conversation_id="conversation-1",
        conversation_type=ConversationType.teams_channel,
        conversation_partition="teams_channel:conversation-1",
    )


def _client_principal(*claims: tuple[str, str]) -> str:
    principal = {
        "auth_typ": "aad",
        "claims": [
            {"typ": claim_type, "val": claim_value}
            for claim_type, claim_value in claims
        ],
    }
    return base64.b64encode(json.dumps(principal).encode()).decode()


def test_dashboard_identity_uses_easyauth_display_name() -> None:
    identity = get_dashboard_identity(
        _client_principal(("name", "Ada Lovelace"))
    )

    assert identity == {
        "authenticated": True,
        "display_name": "Ada Lovelace",
    }


@pytest.mark.parametrize(
    "client_principal",
    [
        None,
        "not base64",
        base64.b64encode(b"[]").decode(),
        base64.b64encode(b'{"claims": null}').decode(),
    ],
)
def test_dashboard_identity_falls_back_to_local_development(
    client_principal: str | None,
) -> None:
    assert get_dashboard_identity(client_principal) == {
        "authenticated": False,
        "display_name": "Local development",
    }


@pytest.mark.asyncio
async def test_list_records_applies_filters_and_pagination() -> None:
    container = _FakeContainer(_record().to_cosmos(), count=101)
    service = QADashboardService()
    start = datetime(2026, 8, 1, tzinfo=timezone.utc)
    end = datetime(2026, 8, 8, tzinfo=timezone.utc)

    with (
        patch(
            "services.qa_dashboard_service.get_qa_records_container",
            new=AsyncMock(return_value=container),
        ),
        patch(
            "services.qa_dashboard_service._load_channel_names",
            new=AsyncMock(
                return_value={
                    "channel-a": "Channel A",
                    "channel-b": "Channel B",
                }
            ),
        ),
        patch(
            "services.qa_dashboard_service._load_conversation_titles",
            new=AsyncMock(
                return_value={
                    "teams_channel:conversation-1": "How do I fix this?"
                }
            ),
        ),
    ):
        result = await service.list_records(
            page=2,
            tenant_id="tenant-a",
            channel_id="channel-a",
            qa_status=QAStatus.failed,
            feedback_status=FeedbackStatusFilter.pending_validation,
            updated_from=start,
            updated_to=end,
            conversation_id="conversation",
        )

    assert result.total == 101
    assert result.page == 2
    assert result.page_size == 50
    assert result.channels == [
        DashboardChannel(id="channel-a", name="Channel A"),
        DashboardChannel(id="channel-b", name="Channel B"),
    ]
    assert result.items[0].id == "teams_channel:conversation-1"
    assert result.items[0].conversation_title == "How do I fix this?"
    assert result.items[0].channel_name == "Channel A"

    records_call = container.calls[1]
    assert records_call["partition_key"] == "tenant-a"
    assert (
        "ORDER BY c.conversation_created_at DESC "
        "OFFSET @offset LIMIT @limit"
    ) in records_call["query"]
    parameters = {
        item["name"]: item["value"] for item in records_call["parameters"]
    }
    assert parameters["@qa_status"] == "failed"
    assert parameters["@channel_id"] == "channel-a"
    assert parameters["@feedback_status"] == "pending_validation"
    assert parameters["@offset"] == 50
    assert parameters["@limit"] == 50


@pytest.mark.asyncio
async def test_list_records_clamps_page_after_total_shrinks() -> None:
    container = _FakeContainer(_record().to_cosmos())
    service = QADashboardService()

    with (
        patch(
            "services.qa_dashboard_service.get_qa_records_container",
            new=AsyncMock(return_value=container),
        ),
        patch(
            "services.qa_dashboard_service._load_channel_names",
            new=AsyncMock(return_value={"channel-a": "Channel A"}),
        ),
        patch(
            "services.qa_dashboard_service._load_conversation_titles",
            new=AsyncMock(return_value={}),
        ),
    ):
        result = await service.list_records(page=2)

    assert result.page == 1
    records_parameters = {
        item["name"]: item["value"]
        for item in container.calls[1]["parameters"]
    }
    assert records_parameters["@offset"] == 0


@pytest.mark.asyncio
async def test_list_records_filters_not_started_feedback() -> None:
    container = _FakeContainer(_record().to_cosmos())
    service = QADashboardService()

    with (
        patch(
            "services.qa_dashboard_service.get_qa_records_container",
            new=AsyncMock(return_value=container),
        ),
        patch(
            "services.qa_dashboard_service._load_channel_names",
            new=AsyncMock(return_value={}),
        ),
        patch(
            "services.qa_dashboard_service._load_conversation_titles",
            new=AsyncMock(return_value={}),
        ),
    ):
        await service.list_records(
            page=1,
            feedback_status=FeedbackStatusFilter.not_started,
        )

    assert "NOT IS_DEFINED(c.feedback) OR IS_NULL(c.feedback)" in (
        container.calls[0]["query"]
    )
    assert "partition_key" not in container.calls[0]


@pytest.mark.asyncio
async def test_get_record_detail_builds_timeline() -> None:
    messages = [
        _message(
            message_id="user-1",
            role=Role.User,
            sender_name="User",
            content="<p>How do I fix this?</p>",
            minute=0,
        ),
        _message(
            message_id="bot-1",
            role=Role.Assistant,
            sender_name="Bot",
            content="Use the old guidance.",
            minute=1,
        ),
    ]
    conversation_service = SimpleNamespace(
        get_messages_by_conversation_id=AsyncMock(return_value=messages),
    )
    with (
        patch(
            "services.qa_dashboard_service.read_qa_record",
            new=AsyncMock(return_value=_record().to_cosmos()),
        ),
        patch(
            "services.qa_dashboard_service._load_channel_names",
            new=AsyncMock(return_value={"channel-a": "Channel A"}),
        ),
    ):
        detail = await QADashboardService(
            conversation_service=conversation_service
        ).get_record_detail(
            record_id="teams_channel:conversation-1",
            tenant_id="tenant-a",
        )

    assert detail is not None
    assert detail.record.conversation_title == "How do I fix this?"
    assert detail.record.channel_name == "Channel A"
    assert detail.record.feedback is not None
    assert (
        detail.record.feedback.issue_url
        == "https://github.com/Azure/azure-sdk-pr/issues/123"
    )


@pytest.mark.asyncio
async def test_list_records_rejects_reversed_time_range() -> None:
    service = QADashboardService()
    with pytest.raises(ValueError, match="updated_from"):
        await service.list_records(
            page=1,
            updated_from=datetime(2026, 8, 8, tzinfo=timezone.utc),
            updated_to=datetime(2026, 8, 7, tzinfo=timezone.utc),
        )


@pytest.mark.asyncio
async def test_dashboard_routes() -> None:
    import server

    page = QARecordPage(
        items=[_dashboard_record()],
        total=1,
        page=1,
        page_size=50,
        channels=[DashboardChannel(id="channel-a", name="Channel A")],
    )
    detail = QADashboardDetail(
        record=_dashboard_record(),
        messages=[],
    )
    service = AsyncMock()
    service.list_records.return_value = page
    service.get_record_detail.return_value = detail

    with patch.object(server, "_qa_dashboard_service", service):
        transport = httpx.ASGITransport(app=server.app)
        async with httpx.AsyncClient(
            transport=transport,
            base_url="http://test",
        ) as client:
            api_response = await client.get(
                "/api/dashboard/qa-records",
                params={
                    "tenant_id": "tenant-a",
                    "channel_id": "channel-a",
                    "qa_status": "failed",
                    "feedback_status": "pending_validation",
                },
            )
            detail_response = await client.get(
                "/api/dashboard/qa-record-details",
                params={
                    "record_id": "teams_channel:conversation-1",
                    "tenant_id": "tenant-a",
                },
            )
            local_user_response = await client.get("/api/dashboard/me")
            authenticated_user_response = await client.get(
                "/api/dashboard/me",
                headers={
                    "x-ms-client-principal": _client_principal(
                        ("name", "Ada Lovelace")
                    )
                },
            )
            page_response = await client.get("/dashboard/qa-records")

    assert api_response.status_code == 200
    assert api_response.json()["total"] == 1
    assert detail_response.status_code == 200
    assert detail_response.json()["record"]["channel_name"] == "Channel A"
    assert local_user_response.json() == {
        "authenticated": False,
        "display_name": "Local development",
    }
    assert authenticated_user_response.json() == {
        "authenticated": True,
        "display_name": "Ada Lovelace",
    }
    assert page_response.status_code == 200
    assert "Chatbot Evolution Dashboard" in page_response.text
    service.list_records.assert_awaited_once()
    service.get_record_detail.assert_awaited_once()


def test_dashboard_html_uses_text_content_for_record_data() -> None:
    html = (
        Path(__file__).resolve().parent.parent
        / "static"
        / "qa_records_dashboard.html"
    ).read_text(encoding="utf-8")
    assert "innerHTML" not in html
    assert "isAllowedIssueUrl" in html
    assert "Conversation status" in html
    assert "Evolution status" in html
    assert "<th>Answer assessment</th>" not in html
    assert "<th>Root cause</th>" not in html
    assert "Bot answered correctly" in html
    assert "Needs evolution" in html
    assert "Bot answered incorrectly" not in html
    assert "Bot answer assessment failed" in html
    assert "Failed (incorrect or unassessed)" in html
    assert ".status-assessment_failed" in html
    assert "Agent report" in html
    assert 'record.feedback?.error !== "agent_remediation_failed"' in html
    assert 'record.feedback.status === "failed"' in html
    assert "setDetailsTitle(record)" in html
    assert "externalLink(record.message_link" in html
    assert "renderMarkdown" in html
    assert 'statusBadge("not_applicable", "/")' in html
    assert "marked@18.0.9" in html
    assert "dompurify@3.4.13" in html
    assert "@highlightjs/cdn-assets@11.11.1" in html
    assert "languages/powershell.min.js" in html
    assert html.count('integrity="sha384-') == 5
    assert html.count('referrerpolicy="no-referrer"') == 5
    assert "DOMPurify.sanitize" in html
    assert "hljs.highlightElement" in html
    assert "AI answer assessment" in html
    assert "Open remediation issue" in html
    assert "Issue details are temporarily unavailable." not in html
    assert "detail.issue" not in html
    assert 'fetch("/api/dashboard/me", {cache: "no-store"})' in html
    assert 'href="/.auth/logout"' in html
    assert "Local development" not in html
