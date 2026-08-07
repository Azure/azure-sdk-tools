"""Read-only Cosmos queries for the QA record dashboard."""

from __future__ import annotations

from datetime import datetime, timezone
from typing import Any

from models.qa_dashboard import FeedbackStatusFilter, QARecordPage
from models.qa_record import QARecord, QAStatus
from utils.azure_cosmosdb import get_qa_records_container

_PAGE_SIZE = 50


class QADashboardService:
    """Query QA records with dashboard filters and server-side pagination."""

    async def list_records(
        self,
        *,
        page: int,
        tenant_id: str | None = None,
        qa_status: QAStatus | None = None,
        feedback_status: FeedbackStatusFilter | None = None,
        updated_from: datetime | None = None,
        updated_to: datetime | None = None,
        conversation_id: str | None = None,
    ) -> QARecordPage:
        if (
            updated_from
            and updated_to
            and _as_utc(updated_from) > _as_utc(updated_to)
        ):
            raise ValueError("updated_from must not be after updated_to")

        conditions, filter_parameters = self._build_filters(
            tenant_id=tenant_id,
            qa_status=qa_status,
            feedback_status=feedback_status,
            updated_from=updated_from,
            updated_to=updated_to,
            conversation_id=conversation_id,
        )
        where_clause = (
            f" WHERE {' AND '.join(conditions)}" if conditions else ""
        )
        query_options: dict[str, Any]
        if tenant_id:
            query_options = {"partition_key": tenant_id}
        else:
            query_options = {"enable_cross_partition_query": True}

        container = await get_qa_records_container()
        count_query = f"SELECT VALUE COUNT(1) FROM c{where_clause}"
        total = 0
        async for value in container.query_items(
            query=count_query,
            parameters=filter_parameters,
            **query_options,
        ):
            count_value: Any = value
            if not isinstance(count_value, (int, float)):
                raise RuntimeError("Cosmos returned a non-numeric QA record count")
            total = int(count_value)
            break

        last_page = max(1, (total + _PAGE_SIZE - 1) // _PAGE_SIZE)
        page = min(page, last_page)
        offset = (page - 1) * _PAGE_SIZE
        records_query = (
            f"SELECT * FROM c{where_clause} "
            "ORDER BY c.updated_at DESC OFFSET @offset LIMIT @limit"
        )
        records_parameters = [
            *filter_parameters,
            {"name": "@offset", "value": offset},
            {"name": "@limit", "value": _PAGE_SIZE},
        ]
        items: list[QARecord] = []
        async for document in container.query_items(
            query=records_query,
            parameters=records_parameters,
            **query_options,
        ):
            items.append(QARecord.from_cosmos(document))

        tenants: list[str] = []
        async for value in container.query_items(
            query=(
                "SELECT DISTINCT VALUE c.tenant_id FROM c "
                "WHERE IS_DEFINED(c.tenant_id)"
            ),
            enable_cross_partition_query=True,
        ):
            if isinstance(value, str) and value:
                tenants.append(value)

        return QARecordPage(
            items=items,
            total=total,
            page=page,
            page_size=_PAGE_SIZE,
            tenants=sorted(set(tenants)),
        )

    @staticmethod
    def _build_filters(
        *,
        tenant_id: str | None,
        qa_status: QAStatus | None,
        feedback_status: FeedbackStatusFilter | None,
        updated_from: datetime | None,
        updated_to: datetime | None,
        conversation_id: str | None,
    ) -> tuple[list[str], list[dict[str, Any]]]:
        conditions: list[str] = []
        parameters: list[dict[str, Any]] = []

        if tenant_id:
            conditions.append("c.tenant_id = @tenant_id")
            parameters.append({"name": "@tenant_id", "value": tenant_id})
        if qa_status:
            conditions.append("c.qa_status = @qa_status")
            parameters.append({"name": "@qa_status", "value": qa_status.value})
        if feedback_status == FeedbackStatusFilter.not_started:
            conditions.append(
                "(NOT IS_DEFINED(c.feedback) OR IS_NULL(c.feedback))"
            )
        elif feedback_status:
            conditions.append("c.feedback.status = @feedback_status")
            parameters.append(
                {"name": "@feedback_status", "value": feedback_status.value}
            )
        if updated_from:
            conditions.append("c.updated_at >= @updated_from")
            parameters.append(
                {
                    "name": "@updated_from",
                    "value": _as_utc_iso(updated_from),
                }
            )
        if updated_to:
            conditions.append("c.updated_at <= @updated_to")
            parameters.append(
                {"name": "@updated_to", "value": _as_utc_iso(updated_to)}
            )
        if conversation_id:
            conditions.append(
                "CONTAINS(c.conversation_id, @conversation_id, true)"
            )
            parameters.append(
                {
                    "name": "@conversation_id",
                    "value": conversation_id.strip(),
                }
            )

        return conditions, parameters


def _as_utc_iso(value: datetime) -> str:
    return _as_utc(value).isoformat()


def _as_utc(value: datetime) -> datetime:
    if value.tzinfo is None:
        value = value.replace(tzinfo=timezone.utc)
    return value.astimezone(timezone.utc)


__all__ = ["QADashboardService"]
