"""Read-only Cosmos queries for the QA record dashboard."""

from __future__ import annotations

import asyncio
from datetime import datetime, timezone
from typing import Any

import yaml

from config import app_config
from models.conversation import (
    ConversationDocumentType,
    ConversationMessageItem,
    Role,
)
from models.qa_dashboard import (
    DashboardChannel,
    DashboardConversationMessage,
    FeedbackStatusFilter,
    QADashboardDetail,
    QADashboardRecord,
    QARecordPage,
)
from models.qa_record import QARecord, QAStatus
from services.conversation_service import ConversationService
from utils.azure_cosmosdb import (
    get_conversation_message_container,
    get_qa_records_container,
    read_qa_record,
)
from utils.azure_storage import download_blob
from utils.text_util import preprocess_html_content

_PAGE_SIZE = 50
_TITLE_LIMIT = 140


class QADashboardService:
    """Query QA records with dashboard filters and server-side pagination."""

    def __init__(
        self,
        *,
        conversation_service: ConversationService | None = None,
    ) -> None:
        self._conversations = conversation_service or ConversationService()

    async def list_records(
        self,
        *,
        page: int,
        tenant_id: str | None = None,
        channel_id: str | None = None,
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
            channel_id=channel_id,
            qa_status=qa_status,
            feedback_status=feedback_status,
            updated_from=updated_from,
            updated_to=updated_to,
            conversation_id=conversation_id,
        )
        where_clause = (
            f" WHERE {' AND '.join(conditions)}" if conditions else ""
        )
        query_options: dict[str, Any] = {}
        if tenant_id:
            query_options["partition_key"] = tenant_id

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
            "ORDER BY c.conversation_created_at DESC "
            "OFFSET @offset LIMIT @limit"
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

        channel_ids: set[str] = {
            item.channel_id for item in items if item.channel_id
        }
        async for value in container.query_items(
            query=(
                "SELECT DISTINCT VALUE c.channel_id FROM c "
                "WHERE IS_DEFINED(c.channel_id) AND NOT IS_NULL(c.channel_id)"
            ),
        ):
            if isinstance(value, str) and value:
                channel_ids.add(value)

        channel_names, titles = await asyncio.gather(
            _load_channel_names(),
            _load_conversation_titles(items),
        )
        dashboard_items = [
            _dashboard_record(
                item,
                title=titles.get(item.id),
                channel_names=channel_names,
            )
            for item in items
        ]
        channels = [
            DashboardChannel(id=value, name=channel_names.get(value, value))
            for value in channel_ids
        ]
        channels.sort(key=lambda item: (item.name.casefold(), item.id))

        return QARecordPage(
            items=dashboard_items,
            total=total,
            page=page,
            page_size=_PAGE_SIZE,
            channels=channels,
        )

    async def get_record_detail(
        self,
        *,
        record_id: str,
        tenant_id: str,
    ) -> QADashboardDetail | None:
        """Build the full conversation and evolution timeline on demand."""
        document = await read_qa_record(
            record_id=record_id,
            tenant_id=tenant_id,
        )
        if document is None:
            return None
        record = QARecord.from_cosmos(document)

        messages, channel_names = await asyncio.gather(
            self._conversations.get_messages_by_conversation_id(
                record.conversation_id,
                record.conversation_type,
            ),
            _load_channel_names(),
        )
        dashboard_record = _dashboard_record(
            record,
            title=_title_from_messages(messages),
            channel_names=channel_names,
        )

        return QADashboardDetail(
            record=dashboard_record,
            messages=_dashboard_messages(messages),
        )

    @staticmethod
    def _build_filters(
        *,
        tenant_id: str | None,
        channel_id: str | None,
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
        if channel_id:
            conditions.append("c.channel_id = @channel_id")
            parameters.append({"name": "@channel_id", "value": channel_id})
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


async def _load_channel_names() -> dict[str, str]:
    container = app_config.get("STORAGE_CONFIG_CONTAINER")
    blob = app_config.get("CHANNEL_CONFIG_BLOB")
    if not container or not blob:
        raise RuntimeError("Storage channel configuration is not configured")
    data = await download_blob(container, blob)
    if not data:
        raise RuntimeError("Storage channel configuration is empty")
    try:
        parsed = yaml.safe_load(data.decode("utf-8")) or {}
    except (UnicodeDecodeError, yaml.YAMLError) as exc:
        raise RuntimeError("Storage channel configuration is invalid") from exc

    names: dict[str, str] = {}
    for entry in parsed.get("channels", []) or []:
        channel_id = entry.get("id")
        name = entry.get("name")
        if isinstance(channel_id, str) and isinstance(name, str):
            names[channel_id] = name
    return names


async def _load_conversation_titles(
    records: list[QARecord],
) -> dict[str, str]:
    if not records:
        return {}
    partitions = [record.id for record in records]
    container = await get_conversation_message_container()
    query = (
        "SELECT c.conversation_partition, c.content, c.created_at FROM c "
        "WHERE c.document_type = @dtype AND c.sender_role = @role "
        "AND ARRAY_CONTAINS(@partitions, c.conversation_partition)"
    )
    parameters: list[dict[str, object]] = [
        {"name": "@dtype", "value": ConversationDocumentType.message.value},
        {"name": "@role", "value": Role.User.value},
        {"name": "@partitions", "value": partitions},
    ]
    first_messages: dict[str, tuple[str, str]] = {}
    async for row in container.query_items(
        query=query,
        parameters=parameters,
    ):
        partition = row.get("conversation_partition")
        content = row.get("content")
        created_at = row.get("created_at")
        if (
            not isinstance(partition, str)
            or not isinstance(content, str)
            or not isinstance(created_at, str)
        ):
            continue
        current = first_messages.get(partition)
        if current is None or created_at < current[0]:
            first_messages[partition] = (created_at, content)
    return {
        partition: _conversation_title(content)
        for partition, (_created_at, content) in first_messages.items()
    }


def _dashboard_record(
    record: QARecord,
    *,
    title: str | None,
    channel_names: dict[str, str],
) -> QADashboardRecord:
    channel_name = (
        channel_names.get(record.channel_id, record.channel_id)
        if record.channel_id
        else None
    )
    return QADashboardRecord(
        **record.model_dump(),
        conversation_title=title or "Untitled conversation",
        channel_name=channel_name or "Unknown channel",
    )


def _title_from_messages(messages: list[ConversationMessageItem]) -> str | None:
    first_user_message = next(
        (message.content for message in messages if message.sender_role == Role.User),
        None,
    )
    return _conversation_title(first_user_message) if first_user_message else None


def _conversation_title(content: str) -> str:
    normalized = " ".join(preprocess_html_content(content).split())
    if len(normalized) <= _TITLE_LIMIT:
        return normalized
    return normalized[: _TITLE_LIMIT - 1].rstrip() + "…"


def _dashboard_messages(
    messages: list[ConversationMessageItem],
) -> list[DashboardConversationMessage]:
    return [
        DashboardConversationMessage(
            id=message.id,
            role=message.sender_role,
            sender_name=message.sender_name,
            content=preprocess_html_content(message.content),
            created_at=message.created_at,
            message_link=(
                message.extra_info.message_link if message.extra_info else None
            ),
            trace_id=message.trace_id,
        )
        for message in messages
    ]


def _as_utc_iso(value: datetime) -> str:
    return _as_utc(value).isoformat()


def _as_utc(value: datetime) -> datetime:
    if value.tzinfo is None:
        value = value.replace(tzinfo=timezone.utc)
    return value.astimezone(timezone.utc)


__all__ = ["QADashboardService"]
