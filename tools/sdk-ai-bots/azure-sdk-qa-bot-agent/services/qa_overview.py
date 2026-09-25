"""Read-only, streaming report aggregation; no evaluator or thread/question join."""

from datetime import datetime, timedelta, timezone
import re
from typing import Any

from models.feedback import RootCauseClassification
from models.qa_dashboard import OverviewCounts, OverviewRow, QAOverview
from utils.channel_policy import is_testing_channel


_ACCURACY_EXCLUDED_CLASSIFICATIONS = (
    RootCauseClassification.missing_content,
    RootCauseClassification.outdated_content,
    RootCauseClassification.insufficient_content,
    RootCauseClassification.out_of_scope,
)


_BOT_MENTION = re.compile(
    r"<at\b[^>]*>Azure SDK Q(?:&amp;|&)A Bot</at>", re.IGNORECASE
)

_ISSUE_URL = re.compile(
    r"https://github\.com/([\w.-]+)/([\w.-]+)/issues/([0-9]+)/?(?:[?#].*)?",
    re.IGNORECASE,
)


def _issue_key(value: Any) -> str | None:
    """Canonical identity for deduplication, without contacting GitHub."""
    if not isinstance(value, str):
        return None
    match = _ISSUE_URL.fullmatch(value.strip())
    if not match:
        return None
    owner, repo, number = match.groups()
    if int(number) <= 0:
        return None
    return f"{owner.casefold()}/{repo.casefold()}/{int(number)}"


def _is_question(document: dict[str, Any]) -> bool:
    """Match the report's user-message eligibility rule, counting the OR once."""
    if document.get("sender_role") != "user":
        return False
    content = document.get("content")
    return document.get("should_reply") is True or (
        isinstance(content, str) and _BOT_MENTION.search(content) is not None
    )


def validate_report_window(start: datetime, end: datetime) -> tuple[datetime, datetime]:
    if any(value.tzinfo is None or value.utcoffset() is None for value in (start, end)):
        raise ValueError("start and end must include a UTC offset")
    start, end = start.astimezone(timezone.utc), end.astimezone(timezone.utc)
    if not timedelta(0) < end - start <= timedelta(days=93):
        raise ValueError("Report range must be positive and at most 93 days")
    return start, end


def _channel(document: dict[str, Any], *, message: bool = False) -> str | None:
    extra = document.get("extra_info") or {} if message else document
    channel_id = extra.get("channel_id")
    if isinstance(channel_id, str) and channel_id:
        return channel_id
    conversation_id = document.get("conversation_id")
    if isinstance(conversation_id, str) and conversation_id:
        return conversation_id.split(";messageid=", 1)[0]
    return None


async def aggregate_overview(
    *, qa_container: Any, message_container: Any, channel_names: dict[str, str],
    start: datetime, end: datetime, channel_id: str | None,
) -> QAOverview:
    """Stream all pages, retaining counts and distinct issue identities, not transcripts."""
    start, end = validate_report_window(start, end)
    parameters: list[dict[str, Any]] = [
        {"name": "@start", "value": start.isoformat()},
        {"name": "@end", "value": end.isoformat()},
    ]
    excluded = {key for key, name in channel_names.items() if is_testing_channel(name)}
    groups: dict[str | None, OverviewRow] = {}
    issues_by_channel: dict[str | None, set[str]] = {}
    all_issues: set[str] = set()

    def row_for(document: dict[str, Any], *, message: bool = False) -> OverviewRow | None:
        channel = _channel(document, message=message)
        if channel in excluded or (channel_id and channel != channel_id):
            return None
        if channel not in groups:
            groups[channel] = OverviewRow(
                channel_id=channel,
                channel_name=(channel_names.get(channel, channel) if channel is not None else None)
                or "Unknown channel",
            )
        return groups[channel]

    qa_query = (
        "SELECT c.channel_id, c.conversation_id, c.conversation_created_at, "
        "c.verdict, c.feedback.classification AS classification, "
        "c.feedback.issue_url AS issue_url, c.feedback.status AS feedback_status, "
        "c.has_expert_interaction FROM c WHERE "
        "c.conversation_created_at >= @start AND c.conversation_created_at < @end"
    )
    async for document in qa_container.query_items(
        query=qa_query, parameters=parameters,
    ):
        # Guard mock/malformed results before they can create an empty channel row.
        if not document.get("conversation_created_at"):
            continue
        row = row_for(document)
        if row is None:
            continue
        row.conversations += 1
        if document.get("classification") in _ACCURACY_EXCLUDED_CLASSIFICATIONS:
            row.accuracy_excluded += 1
        elif document.get("verdict") == "correct":
            row.correct += 1
        elif document.get("verdict") == "incorrect":
            row.incorrect += 1
        expert = document.get("has_expert_interaction")
        if expert is True:
            row.expert_yes += 1

        classification = document.get("classification")
        if isinstance(classification, str) and classification in RootCauseClassification:
            root_cause = RootCauseClassification(classification)
            row.findings += 1
            row.root_causes[root_cause] = row.root_causes.get(root_cause, 0) + 1
        issue_key = _issue_key(document.get("issue_url"))
        if issue_key is not None:
            row.issue_cases += 1
            issues_by_channel.setdefault(row.channel_id, set()).add(issue_key)
            all_issues.add(issue_key)
            status = document.get("feedback_status")
            if status == "validation_passed":
                row.resolved_cases += 1
            elif status == "pending_validation":
                row.pending_validation_cases += 1
            elif status == "validation_failed":
                row.validation_failed_cases += 1
            elif status == "validation_skipped":
                row.validation_skipped_cases += 1
            elif status == "failed":
                row.processing_error_cases += 1
            else:
                row.other_issue_cases += 1

    # Do not join to QA records: unanswered questions have no QA record.
    # Replies have no originating question ID. Infer an answer to the
    # latest eligible question in the same thread, once, within this window.
    # Keep only one pending question per thread, never the message transcripts.
    pending_questions: dict[tuple[str | None, str], tuple[str, OverviewRow]] = {}
    reply_threads: set[tuple[str | None, str]] = set()
    batch_timestamp: str | None = None

    def count_answered_questions() -> None:
        # Cosmos does not guarantee order among equal timestamps. Defer replies
        # until all questions at that timestamp are known; never pair a tie.
        for key in reply_threads:
            pending = pending_questions.get(key)
            if pending is not None and batch_timestamp is not None and pending[0] < batch_timestamp:
                pending_questions.pop(key)
                pending[1].answered_questions += 1
        reply_threads.clear()

    message_query = (
        "SELECT c.extra_info, c.conversation_id, c.conversation_partition, "
        "c.conversation_type, c.created_at, c.sender_role, c.should_reply, c.content "
        "FROM c WHERE c.document_type = 'conversation_message' "
        "AND c.created_at >= @start AND c.created_at < @end "
        "AND c.sender_role IN ('user', 'assistant', 'system') "
        "ORDER BY c.created_at ASC"
    )
    async for document in message_container.query_items(
        query=message_query, parameters=parameters,
    ):
        is_bot_reply = document.get("sender_role") in ("assistant", "system")
        if not is_bot_reply and not _is_question(document):
            continue
        row = row_for(document, message=True)
        if row is None:
            continue
        if not is_bot_reply:
            row.questions += 1

        thread = document.get("conversation_partition")
        if not thread and document.get("conversation_id"):
            thread = f"{document.get('conversation_type') or 'teams_channel'}:{document['conversation_id']}"
        created_at = document.get("created_at")
        if not isinstance(thread, str) or not thread or not isinstance(created_at, str):
            # Keep unmatchable questions in the denominator, but do not guess
            # their answers by grouping unrelated messages at channel level.
            continue
        if created_at != batch_timestamp:
            count_answered_questions()
            batch_timestamp = created_at
        key = (row.channel_id, thread)
        if not is_bot_reply:
            pending_questions[key] = (created_at, row)
        else:
            reply_threads.add(key)
    count_answered_questions()

    rows = sorted(groups.values(), key=lambda row: (
        row.channel_name.casefold(), row.channel_id or "",
    ))
    for row in rows:
        row.tracked_issues = len(issues_by_channel.get(row.channel_id, set()))
    totals = OverviewRow.model_validate({
        "channel_name": "Total",
        "tracked_issues": len(all_issues),
        "root_causes": {
            cause: sum(row.root_causes.get(cause, 0) for row in rows)
            for cause in RootCauseClassification
            if any(cause in row.root_causes for row in rows)
        },
        **{
            name: sum(getattr(row, name) for row in rows)
            for name in OverviewCounts.model_fields
        },
    })
    return QAOverview(
        start=start, end=end, generated_at=datetime.now(timezone.utc),
        channel_id=channel_id, rows=rows, totals=totals,
    )