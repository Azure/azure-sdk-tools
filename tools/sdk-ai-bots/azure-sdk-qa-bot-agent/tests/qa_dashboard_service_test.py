"""Tests for the QA record dashboard query and routes."""

from __future__ import annotations

from datetime import datetime, timezone
from pathlib import Path
from unittest.mock import AsyncMock, patch

import httpx
import pytest

from models.conversation import ConversationType
from models.qa_dashboard import FeedbackStatusFilter, QARecordPage
from models.qa_record import FeedbackState, FeedbackStatus, QARecord, QAStatus
from services.qa_dashboard_service import QADashboardService


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
        elif "DISTINCT VALUE c.tenant_id" in query:
            yield "tenant-b"
            yield "tenant-a"
        else:
            yield self.document


def _record() -> QARecord:
    now = datetime(2026, 8, 7, tzinfo=timezone.utc)
    return QARecord(
        id="teams_channel:conversation-1",
        tenant_id="tenant-a",
        conversation_id="conversation-1",
        conversation_type=ConversationType.teams_channel,
        qa_status=QAStatus.failed,
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


@pytest.mark.asyncio
async def test_list_records_applies_filters_and_pagination() -> None:
    container = _FakeContainer(_record().to_cosmos(), count=101)
    service = QADashboardService()
    start = datetime(2026, 8, 1, tzinfo=timezone.utc)
    end = datetime(2026, 8, 8, tzinfo=timezone.utc)

    with patch(
        "services.qa_dashboard_service.get_qa_records_container",
        new=AsyncMock(return_value=container),
    ):
        result = await service.list_records(
            page=2,
            tenant_id="tenant-a",
            qa_status=QAStatus.failed,
            feedback_status=FeedbackStatusFilter.pending_validation,
            updated_from=start,
            updated_to=end,
            conversation_id="conversation",
        )

    assert result.total == 101
    assert result.page == 2
    assert result.page_size == 50
    assert result.tenants == ["tenant-a", "tenant-b"]
    assert result.items[0].id == "teams_channel:conversation-1"

    records_call = container.calls[1]
    assert records_call["partition_key"] == "tenant-a"
    assert "ORDER BY c.updated_at DESC OFFSET @offset LIMIT @limit" in (
        records_call["query"]
    )
    parameters = {
        item["name"]: item["value"] for item in records_call["parameters"]
    }
    assert parameters["@qa_status"] == "failed"
    assert parameters["@feedback_status"] == "pending_validation"
    assert parameters["@offset"] == 50
    assert parameters["@limit"] == 50


@pytest.mark.asyncio
async def test_list_records_clamps_page_after_total_shrinks() -> None:
    container = _FakeContainer(_record().to_cosmos())
    service = QADashboardService()

    with patch(
        "services.qa_dashboard_service.get_qa_records_container",
        new=AsyncMock(return_value=container),
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

    with patch(
        "services.qa_dashboard_service.get_qa_records_container",
        new=AsyncMock(return_value=container),
    ):
        await service.list_records(
            page=1,
            feedback_status=FeedbackStatusFilter.not_started,
        )

    assert "NOT IS_DEFINED(c.feedback) OR IS_NULL(c.feedback)" in (
        container.calls[0]["query"]
    )
    assert container.calls[0]["enable_cross_partition_query"] is True


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
        items=[_record()],
        total=1,
        page=1,
        page_size=50,
        tenants=["tenant-a"],
    )
    service = AsyncMock()
    service.list_records.return_value = page

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
                    "qa_status": "failed",
                    "feedback_status": "pending_validation",
                },
            )
            page_response = await client.get("/dashboard/qa-records")

    assert api_response.status_code == 200
    assert api_response.json()["total"] == 1
    assert page_response.status_code == 200
    assert "QA Record Dashboard" in page_response.text
    service.list_records.assert_awaited_once()


def test_dashboard_html_uses_text_content_for_record_data() -> None:
    html = (
        Path(__file__).resolve().parent.parent
        / "static"
        / "qa_records_dashboard.html"
    ).read_text(encoding="utf-8")
    assert "detailsJson.textContent" in html
    assert "innerHTML" not in html
    assert "isAllowedIssueUrl" in html
