"""Offline report tests: real aggregation over async, query-aware fake containers."""

from datetime import datetime, timedelta, timezone
import json
from pathlib import Path
import re
from unittest.mock import AsyncMock

import httpx
import pytest

from models.qa_dashboard import OverviewCounts, OverviewRow
from models.feedback import RootCauseClassification
from services.qa_dashboard_service import QADashboardService
from services.qa_overview import validate_report_window

START = datetime(2026, 9, 7, tzinfo=timezone.utc)
END = START + timedelta(days=7)


class Container:
    def __init__(self, documents, *, messages=False):
        self.documents = documents
        self.messages = messages
        self.calls = []

    async def query_items(self, **kwargs):
        self.calls.append(kwargs)
        params = {p["name"]: p["value"] for p in kwargs["parameters"]}
        start = datetime.fromisoformat(params["@start"])
        end = datetime.fromisoformat(params["@end"])
        documents = self.documents
        if self.messages and "ORDER BY c.created_at ASC" in kwargs["query"]:
            documents = sorted(documents, key=lambda doc: doc.get("created_at") or "")
        for doc in documents:
            if self.messages and (
                doc.get("document_type") != "conversation_message"
                or doc.get("sender_role") not in ("user", "assistant", "system")
            ):
                continue
            timestamp = doc.get("created_at" if self.messages else "conversation_created_at")
            if not timestamp:
                continue
            if not start <= datetime.fromisoformat(timestamp) < end:
                continue
            if not self.messages and "feedback" in doc:
                feedback = doc["feedback"] or {}
                yield {**doc, "classification": feedback.get("classification"),
                       "issue_url": feedback.get("issue_url"), "feedback_status": feedback.get("status")}
            else:
                yield doc


def qa(channel="a", verdict="correct", expert=False, **changes):
    return {
        "tenant_id": "tenant", "channel_id": channel,
        "conversation_created_at": START.isoformat(),
        "verdict": verdict, "has_expert_interaction": expert,
        **changes,
    }


def message(channel="a", role="user", should_reply=True, **changes):
    return {
        "tenant_id": "tenant", "extra_info": {"channel_id": channel},
        "conversation_id": f"{channel};messageid=thread",
        "created_at": (START + timedelta(seconds=int(role in ("assistant", "system")))).isoformat(),
        "sender_role": role,
        "document_type": "conversation_message", "should_reply": should_reply,
        **changes,
    }


@pytest.fixture
def storage(monkeypatch):
    qa_container = Container([])
    messages = Container([], messages=True)
    monkeypatch.setattr("services.qa_dashboard_service.get_qa_records_container", AsyncMock(return_value=qa_container))
    monkeypatch.setattr("services.qa_dashboard_service.get_conversation_message_container", AsyncMock(return_value=messages))
    monkeypatch.setattr("services.qa_dashboard_service._load_channel_names", AsyncMock(return_value={
        "a": "Channel A", "b": "Channel B", "test": " SDK Testing ",
        "smoke": "smoke-tests", "auto": "Azure SDK QA Bot - Auto Reply - Test",
    }))
    return qa_container, messages


@pytest.mark.asyncio
async def test_full_aggregation_and_weighted_totals(storage):
    records, messages = storage
    # >50 documents ensures no dependence on the list's page size.
    records.documents = [qa() for _ in range(90)] + [qa("b", "incorrect", True) for _ in range(10)]
    messages.documents = [message(conversation_id=f"a;messageid={i}") for i in range(100)] + [
        message(role="assistant", conversation_id=f"a;messageid={i}") for i in range(98)
    ]
    result = await QADashboardService().get_overview(start=START, end=END)
    assert len(result.rows) == 2
    assert result.totals.conversations == 100
    assert result.totals.accuracy.rate == 90  # NOT (100 + 0) / 2
    assert result.totals.expert_interaction.rate == 10
    assert result.totals.answer_rate.rate == 98
    for metric, numerator in (
        (result.totals.accuracy, 90),
        (result.totals.expert_interaction, 10),
        (result.totals.answer_rate, 98),
    ):
        assert metric.numerator == numerator and metric.denominator == 100
        assert set(metric.model_dump()) == {"numerator", "denominator", "rate"}
    assert result.rows[1].answer_rate.rate is None
    assert all("OFFSET" not in call["query"] and "LIMIT" not in call["query"] for call in records.calls + messages.calls)
    assert "updated_at" not in records.calls[0]["query"]
    assert "has_expert_reply" not in records.calls[0]["query"]


@pytest.mark.asyncio
async def test_unknowns_exclusions_and_legacy_are_not_inferred(storage):
    records, messages = storage
    legacy = qa(verdict=None, expert=None, has_expert_reply=True)
    legacy.pop("has_expert_interaction")
    records.documents = [
        qa(), legacy, qa(verdict="unknown", expert=None),
        qa(verdict="incorrect", expert=True, classification="missing_content"),
        qa(verdict="incorrect", expert=False, classification="out_of_scope"),
        qa(verdict="incorrect", expert=None, classification="insufficient_content"),
        qa(conversation_created_at=None),
    ]
    messages.documents = [message(), message(should_reply=None), message(should_reply=False), message(role="system")]
    result = await QADashboardService().get_overview(start=START, end=END)
    total = result.totals
    assert total.conversations == 6
    assert total.accuracy_excluded == 3
    assert total.correct == 1 and total.incorrect == 0
    assert total.accuracy.numerator == 3 and total.accuracy.denominator == 3
    assert total.accuracy.rate == 100
    assert total.expert_interaction.denominator == 6
    assert total.expert_interaction.rate == pytest.approx(100 / 6)
    assert total.expert_yes == 1
    assert total.answer_rate.rate == 100
    assert total.answer_rate.numerator == total.answer_rate.denominator == 1
    assert "scope_unknown" not in total.model_dump()
    assert "out_of_scope_messages" not in total.model_dump()


@pytest.mark.asyncio
async def test_dates_are_half_open_and_based_on_creation_not_updates(storage):
    records, messages = storage
    before = START - timedelta(microseconds=1)
    inside = END - timedelta(microseconds=1)
    records.documents = [qa(conversation_created_at=value.isoformat(), updated_at=END.isoformat()) for value in (before, START, inside, END)]
    records.documents.append(qa(updated_at=(END + timedelta(days=10)).isoformat()))
    messages.documents = [message(created_at=value.isoformat()) for value in (before, START, inside, END)]
    result = await QADashboardService().get_overview(
        start=START.astimezone(timezone(timedelta(hours=8))),
        end=END.astimezone(timezone(timedelta(hours=8))),
    )
    assert result.totals.conversations == 3
    assert result.totals.questions == 2
    assert result.start == START and result.end == END
    for call in records.calls + messages.calls:
        params = {p["name"]: p["value"] for p in call["parameters"]}
        assert params["@start"] == START.isoformat()
        assert params["@end"] == END.isoformat()
        assert "< @end" in call["query"] and ">= @start" in call["query"]
        assert "<= @end" not in call["query"]
    assert records.calls[0]["query"].split(" WHERE ", 1)[1] == (
        "c.conversation_created_at >= @start AND c.conversation_created_at < @end"
    )


@pytest.mark.asyncio
async def test_answer_rate_counts_questions_once_not_bot_replies(storage):
    records, messages = storage
    messages.documents = [
        message("unanswered"), message("unanswered"), message("unanswered", should_reply=False),
        message("a", role="assistant", created_at=START.isoformat()),
        message("a", role="system", created_at=START.isoformat()),
        message("a", role="developer"), message("a", document_type="conversation_mapping"),
        message("a", created_at=(START + timedelta(seconds=1)).isoformat()),
        message("a", role="assistant", created_at=(START + timedelta(seconds=2)).isoformat()),
        message("a", role="system", created_at=(START + timedelta(seconds=3)).isoformat()),
    ]
    result = await QADashboardService().get_overview(start=START, end=END)
    assert result.totals.conversations == 0
    unanswered = next(row for row in result.rows if row.channel_id == "unanswered")
    assert unanswered.questions == 2 and unanswered.answer_rate.rate == 0
    answered = next(row for row in result.rows if row.channel_id == "a")
    assert answered.answered_questions == 1 and answered.answer_rate.rate == 100
    assert result.totals.answer_rate.model_dump() == {
        "numerator": 1, "denominator": 3, "rate": pytest.approx(100 / 3),
    }


@pytest.mark.parametrize("reverse", [False, True])
@pytest.mark.asyncio
async def test_answer_rate_matches_latest_eligible_question_in_each_thread(storage, reverse):
    def turn(second, *, thread="one", role="user", **changes):
        return message(role=role, conversation_id=thread,
                       created_at=(START + timedelta(seconds=second)).isoformat(), **changes)

    documents = [
        turn(0, role="assistant"),  # An orphan reply cannot answer a future question.
        turn(1), turn(2),  # One response must not credit both questions.
        turn(3, thread="two"),
        turn(4, should_reply=False),  # A non-question does not replace the pending question.
        turn(5, role="assistant"), turn(6, role="system"),
        turn(7), turn(8, role="system"),
        turn(9, thread="other", role="assistant"),
    ]
    storage[1].documents = documents[::-1] if reverse else documents
    total = (await QADashboardService().get_overview(start=START, end=END)).totals
    assert total.questions == 4
    assert total.answered_questions == 2
    assert total.answer_rate.model_dump() == {"numerator": 2, "denominator": 4, "rate": 50}
    assert "ORDER BY c.created_at ASC" in storage[1].calls[0]["query"]


@pytest.mark.asyncio
async def test_answer_rate_does_not_pair_missing_or_different_thread_identities(storage):
    storage[1].documents = [
        message(conversation_id=None), message(role="assistant", conversation_id=None),
        message(conversation_id="same", conversation_partition="partition-one"),
        message(role="assistant", conversation_id="same", conversation_partition="partition-two"),
        message("b", conversation_id="same", conversation_partition="partition-one", role="assistant"),
        message(conversation_id="typed", conversation_type="one"),
        message(role="assistant", conversation_id="typed", conversation_type="two"),
        message(conversation_id=None, conversation_partition="valid"),
        message(role="assistant", conversation_id=None, conversation_partition="valid"),
    ]
    total = (await QADashboardService().get_overview(start=START, end=END)).totals
    assert total.answer_rate.model_dump() == {"numerator": 1, "denominator": 4, "rate": 25}


@pytest.mark.asyncio
async def test_answer_rate_only_matches_questions_and_replies_inside_window(storage):
    storage[1].documents = [
        message(conversation_id="before", created_at=(START - timedelta(seconds=1)).isoformat()),
        message(role="assistant", conversation_id="before", created_at=START.isoformat()),
        message(conversation_id="after", created_at=(END - timedelta(seconds=1)).isoformat()),
        message(role="assistant", conversation_id="after", created_at=END.isoformat()),
        message(conversation_id="inside", created_at=START.isoformat()),
        message(role="system", conversation_id="inside", created_at=(END - timedelta(seconds=1)).isoformat()),
        message(conversation_id="tie", created_at=START.isoformat()),
        message(role="assistant", conversation_id="tie", created_at=START.isoformat()),
    ]
    total = (await QADashboardService().get_overview(start=START, end=END)).totals
    assert total.answer_rate.model_dump() == {
        "numerator": 1, "denominator": 3, "rate": pytest.approx(100 / 3),
    }


@pytest.mark.parametrize("questions,answered,rate", [(0, 0, None), (2, 0, 0), (4, 3, 75), (1, 1, 100)])
def test_answer_rate_uses_answered_questions_not_raw_reply_count(questions, answered, rate):
    row = OverviewRow(channel_name="Test", questions=questions, answered_questions=answered)
    assert "bot_replies" not in OverviewCounts.model_fields
    assert "bot_replies" not in row.model_dump()
    assert row.answer_rate.model_dump() == {"numerator": answered, "denominator": questions, "rate": rate}


@pytest.mark.parametrize("reverse", [False, True])
@pytest.mark.asyncio
async def test_answer_rate_timestamp_ties_do_not_depend_on_storage_order(storage, reverse):
    same_time = (START + timedelta(seconds=1)).isoformat()
    documents = [message(), message(created_at=same_time), message(role="assistant", created_at=same_time)]
    storage[1].documents = documents[::-1] if reverse else documents
    total = (await QADashboardService().get_overview(start=START, end=END)).totals
    assert total.answer_rate.model_dump() == {"numerator": 0, "denominator": 2, "rate": 0}


@pytest.mark.asyncio
async def test_scopes_channel_fallback_and_shared_test_policy(storage):
    records, messages = storage
    records.documents = [qa(channel) for channel in ("a", "b", "test", "smoke", "auto")]
    records.documents += [qa("a", tenant_id="other"), qa(None, conversation_id="test;messageid=123")]
    messages.documents = [message(channel) for channel in ("a", "b", "test", "smoke", "auto")]
    messages.documents += [message("a", tenant_id=None), message("a", tenant_id="other"), message(extra_info=None, conversation_id="test;messageid=123")]
    report = await QADashboardService().get_overview(start=START, end=END)
    assert report.totals.conversations == 3
    assert report.totals.questions == 4
    assert [row.channel_id for row in report.rows] == ["a", "b"]
    assert report.rows[0].conversations == 2
    assert report.rows[0].questions == 3
    scoped = await QADashboardService().get_overview(start=START, end=END, channel_id="a")
    assert len(scoped.rows) == 1
    assert scoped.totals.conversations == scoped.rows[0].conversations == 2
    assert scoped.totals.questions == scoped.rows[0].questions == 3
    excluded = await QADashboardService().get_overview(start=START, end=END, channel_id="test")
    assert excluded.rows == []
    for call in records.calls + messages.calls:
        assert "partition_key" not in call
        assert "tenant_id" not in call["query"]
        assert {p["name"] for p in call["parameters"]} == {"@start", "@end"}


@pytest.mark.parametrize("tenant_fields", [{}, {"tenant_id": None}, {"tenant_id": "other"}])
@pytest.mark.asyncio
async def test_same_channel_consolidates_all_stored_tenants(storage, tenant_fields):
    records, messages = storage
    legacy_record, legacy_message = qa(), message()
    for document in (legacy_record, legacy_message):
        document.pop("tenant_id")
        document.update(tenant_fields)
    records.documents = [qa(), legacy_record]
    messages.documents = [message(), legacy_message]
    result = await QADashboardService().get_overview(start=START, end=END, channel_id="a")
    assert len(result.rows) == 1
    assert result.rows[0].conversations == result.totals.conversations == 2
    assert result.rows[0].questions == result.totals.questions == 2
    data = result.model_dump(mode="json")
    assert "tenant_id" not in data
    assert all("tenant_id" not in row for row in [*data["rows"], data["totals"]])


@pytest.mark.asyncio
async def test_rows_sort_by_channel_name_then_id(storage, monkeypatch):
    monkeypatch.setattr("services.qa_dashboard_service._load_channel_names", AsyncMock(return_value={
        "a": "Shared", "b": "shared", "c": "Alpha",
    }))
    storage[0].documents = [qa("b"), qa("a"), qa("c")]
    result = await QADashboardService().get_overview(start=START, end=END)
    assert [row.channel_id for row in result.rows] == ["c", "a", "b"]


@pytest.mark.asyncio
async def test_unknown_channel_messages_remain_visible_without_dated_conversations(storage):
    records, messages = storage
    records.documents = [qa(None, conversation_created_at=None)]
    messages.documents = [message(extra_info=None, conversation_id=None)]
    result = await QADashboardService().get_overview(start=START, end=END)
    assert len(result.rows) == 1
    assert result.rows[0].channel_name == "Unknown channel"
    assert result.totals.questions == 1
    assert result.totals.conversations == 0
    assert result.totals.accuracy.rate is None


@pytest.mark.parametrize("date_fields", [{}, {"conversation_created_at": None}, {"conversation_created_at": ""}])
@pytest.mark.parametrize("dated_record", [False, True])
@pytest.mark.asyncio
async def test_undated_mock_results_never_create_rows_or_change_metrics(storage, monkeypatch, date_fields, dated_record):
    records, _ = storage
    documents = [qa()] if dated_record else []
    for channel in ("a", "undated-only", None):
        document = qa(channel, verdict="incorrect", expert=True)
        document.pop("conversation_created_at")
        document.update(date_fields)
        documents.append(document)

    async def unfiltered_query(**kwargs):
        records.calls.append(kwargs)
        for document in documents:
            yield document

    monkeypatch.setattr(records, "query_items", unfiltered_query)
    result = await QADashboardService().get_overview(start=START, end=END)
    assert [row.channel_id for row in result.rows] == (["a"] if dated_record else [])
    expected = OverviewRow(
        channel_name="Total", conversations=int(dated_record),
        correct=int(dated_record),
    )
    assert result.totals.model_dump() == expected.model_dump()


@pytest.mark.asyncio
async def test_empty_report_is_na(storage):
    result = await QADashboardService().get_overview(start=START, end=END)
    assert result.rows == []
    for metric in (result.totals.accuracy, result.totals.expert_interaction, result.totals.answer_rate, result.totals.resolved_rate):
        assert metric.rate is None
        assert metric.numerator == metric.denominator == 0


@pytest.mark.parametrize("conversations, expert_yes, rate", [
    (2, 1, 50), (1, 0, 0), (2, 0, 0), (0, 0, None),
])
def test_expert_rate_uses_all_conversations(conversations, expert_yes, rate):
    row = OverviewRow(channel_name="Test", conversations=conversations, expert_yes=expert_yes)
    assert row.expert_interaction.model_dump() == {
        "numerator": expert_yes, "denominator": conversations, "rate": rate,
    }


@pytest.mark.parametrize("changes, numerator, denominator, rate", [
    ({}, 0, 0, None),
    ({"conversations": 10}, 10, 10, 100),
    ({"conversations": 10, "incorrect": 2}, 8, 10, 80),
    ({"conversations": 10, "incorrect": 10}, 0, 10, 0),
    ({"conversations": 10, "accuracy_excluded": 10}, 0, 0, None),
    ({"conversations": 10, "accuracy_excluded": 2, "incorrect": 3}, 5, 8, 62.5),
])
def test_accuracy_removes_exclusions_from_numerator_and_denominator(changes, numerator, denominator, rate):
    row = OverviewRow(channel_name="Test", **changes)
    assert row.accuracy.model_dump() == {
        "numerator": numerator, "denominator": denominator, "rate": rate,
    }


@pytest.mark.parametrize("verdict", ["correct", "incorrect", "unknown", None])
@pytest.mark.parametrize("classification", ["missing_content", "outdated_content", "insufficient_content", "out_of_scope"])
@pytest.mark.asyncio
async def test_excluded_only_channels_have_na_accuracy_without_hiding_other_metrics(storage, verdict, classification):
    storage[0].documents = [
        qa("a", verdict=verdict, expert=True, feedback=issue_feedback(classification=classification)),
        qa("b", verdict=verdict, feedback=issue_feedback(classification=classification)),
    ]
    report = await QADashboardService().get_overview(start=START, end=END)
    for row in [*report.rows, report.totals]:
        assert row.accuracy.model_dump() == {"numerator": 0, "denominator": 0, "rate": None}
        assert row.correct == row.incorrect == 0
        assert row.conversations == row.accuracy_excluded
    assert report.totals.conversations == report.totals.findings == report.totals.issue_cases == 2
    assert report.totals.expert_interaction.model_dump() == {"numerator": 1, "denominator": 2, "rate": 50}


@pytest.mark.asyncio
async def test_accuracy_totals_weight_eligible_conversations_not_channel_rates(storage):
    storage[0].documents = [qa("a") for _ in range(3)] + [
        qa("a", verdict="incorrect", classification="missing_content"),
        qa("a", verdict="incorrect", classification="outdated_content"),
        qa("b", verdict="incorrect"),
        qa("b", verdict="incorrect", classification="insufficient_content"),
        qa("b", verdict="incorrect", classification="out_of_scope"),
    ]
    report = await QADashboardService().get_overview(start=START, end=END)
    assert [row.accuracy.rate for row in report.rows] == [100, 0]
    assert report.totals.accuracy.model_dump() == {"numerator": 3, "denominator": 4, "rate": 75}


@pytest.mark.asyncio
async def test_unassessed_conversations_produce_full_accuracy_without_expert_interaction(storage):
    storage[0].documents = [qa(verdict=None, expert=None) for _ in range(10)]
    result = await QADashboardService().get_overview(start=START, end=END)
    assert result.totals.correct == result.totals.incorrect == 0
    assert result.totals.accuracy.rate == 100
    assert result.totals.expert_interaction.rate == 0
    assert result.totals.accuracy.denominator == result.totals.expert_interaction.denominator == 10


@pytest.mark.parametrize("changes", [
    {},
    {"conversations": 10, "correct": 9, "incorrect": 1, "expert_yes": 1, "questions": 100, "answered_questions": 98},
    {"conversations": 1},
    {"conversations": 1, "correct": 1, "questions": 1, "answered_questions": 1},
])
def test_serialized_metrics_have_no_goal(changes):
    data = OverviewRow(channel_name="Test", **changes).model_dump(mode="json")
    assert "undated_conversations" not in OverviewCounts.model_fields
    assert "undated_conversations" not in OverviewRow.model_fields
    assert "undated_conversations" not in data
    for removed in ("correctness_unknown", "expert_no", "expert_unknown"):
        assert removed not in OverviewCounts.model_fields
        assert removed not in data
    for name in ("accuracy", "expert_interaction", "answer_rate", "resolved_rate"):
        assert "goal" not in data[name]
        assert set(data[name]) == {"numerator", "denominator", "rate"}


def test_expert_instruction_and_example_match_boolean_metric_semantics():
    instruction = (Path(__file__).resolve().parent.parent / "agents/chatbot_evolution_agent/instruction.md").read_text(encoding="utf-8")
    assessment = instruction.split("2. **Assess expert interaction.**", 1)[1].split("3. **Decide", 1)[0]
    assert assessment.index("**If**") < assessment.index("**Else if**") < assessment.index("**Else**")
    assert "no message qualifies, set `false`" in assessment
    assert "only author follow-ups or confirmations" in assessment
    assert "set `null`: the available evidence cannot establish" in assessment
    assert all(line.lstrip().startswith(("- ", "Give ")) for line in assessment.splitlines()[1:] if line.strip())
    example = json.loads(re.search(r"```json\s*(.*?)\s*```", instruction, re.DOTALL).group(1))
    assert example["has_expert_interaction"] is None
    assert "Insufficient evidence" in example["expert_interaction_reason"]


def test_expert_assessment_requires_added_value_not_technical_confirmation():
    root = Path(__file__).resolve().parent.parent
    instruction = " ".join((root / "agents/chatbot_evolution_agent/instruction.md").read_text(encoding="utf-8").split())
    assert "adds meaningful information beyond the bot's answer" in instruction
    assert "Compare with the preceding bot answer" in instruction
    assert "confirmation or repetition alone does not count, even when technically substantive" in instruction
    assert "identifying what was added beyond the bot's answer when `true`" in instruction
    html = (root / "static/qa_records_dashboard.html").read_text(encoding="utf-8")
    assert "Expert follow-up adds guidance beyond the bot's answer" in html
    assert "adds guidance beyond the bot's answer" in html


@pytest.mark.asyncio
async def test_confirmation_only_assessment_does_not_increase_expert_rate(storage):
    # Given a false semantic assessment, reporting must not infer interaction
    # from a technical confirmation or the older expert-reply heuristic.
    storage[0].documents = [qa(expert=False, has_expert_reply=True,
        expert_interaction_reason=(
            "Another human only confirmed the bot's answer: Making additive changes "
            "to an existing API is expressly disallowed and enforced by breaking change checks."
        ))]
    total = (await QADashboardService().get_overview(start=START, end=END)).totals
    assert total.expert_yes == 0
    assert total.expert_interaction.model_dump() == {"numerator": 0, "denominator": 1, "rate": 0}


@pytest.mark.parametrize("start,end", [
    (START, START), (END, START), (START, START + timedelta(days=94)),
    (START.replace(tzinfo=None), END), (START, END.replace(tzinfo=None)),
])
@pytest.mark.asyncio
async def test_invalid_windows_fail_before_storage(storage, start, end):
    with pytest.raises(ValueError):
        await QADashboardService().get_overview(start=start, end=end)
    assert storage[0].calls == storage[1].calls == []


def test_maximum_window_allowed():
    assert validate_report_window(START, START + timedelta(days=93))[0] == START


def issue_feedback(status="validation_passed", number=1, classification="reasoning_gap"):
    return {"status": status, "issue_url": f"https://github.com/Azure/example/issues/{number}",
            "classification": classification}


@pytest.mark.asyncio
async def test_issue_resolution_is_case_based_and_globally_deduplicated(storage):
    records, _ = storage
    records.documents = [qa(feedback=issue_feedback()) for _ in range(9)] + [
        qa("b", feedback=issue_feedback("pending_validation")),
        qa("b", feedback=issue_feedback("validation_failed", number=2)),
        qa("b", feedback=issue_feedback("validation_skipped", number=3)),
        qa("b", feedback=issue_feedback("failed", number=4)),
        qa("b", feedback=issue_feedback("running", number=5)),
        qa("b", feedback=issue_feedback("done", number=6)),
        qa("b", feedback={"classification": "missing_content"}),
        qa("b", feedback=None),
    ]
    report = await QADashboardService().get_overview(start=START, end=END)
    total = report.totals
    assert total.findings == 16
    assert total.issue_cases == 15
    assert total.tracked_issues == 6  # Shared issue 1 is counted only once globally.
    assert sum(row.tracked_issues for row in report.rows) == 7
    assert total.resolved_cases == 9 and total.unresolved_cases == 5
    assert total.resolved_rate.model_dump() == {"numerator": 9, "denominator": 14, "rate": pytest.approx(900 / 14)}
    assert [row.resolved_rate.rate for row in report.rows] == [100, 0]
    assert total.pending_validation_cases == total.validation_failed_cases == 1
    assert total.validation_skipped_cases == total.processing_error_cases == 1
    assert total.other_issue_cases == 2
    assert total.unresolved_cases == sum(getattr(total, name) for name in (
        "pending_validation_cases", "validation_failed_cases",
        "processing_error_cases", "other_issue_cases",
    ))
    for row in [*report.rows, total]:
        assert row.issue_cases == row.resolved_cases + row.validation_skipped_cases + row.unresolved_cases
        assert row.resolved_rate.denominator == row.resolved_cases + row.unresolved_cases
    assert total.resolved_rate.denominator == sum(row.resolved_rate.denominator for row in report.rows)
    assert total.root_causes == {"reasoning_gap": 15, "missing_content": 1}
    query = records.calls[0]["query"]
    assert "c.feedback.issue_url AS issue_url" in query
    assert "c.feedback.status AS feedback_status" in query


@pytest.mark.parametrize("status, resolved, other", [
    ("validation_passed", 1, 0), ("validation_failed", 0, 0),
    ("pending_validation", 0, 0), ("validation_skipped", 0, 0), ("failed", 0, 0),
    ("created", 0, 1), ("running", 0, 1), ("done", 0, 1),
    ("closed", 0, 1), (None, 0, 1), ("unknown", 0, 1),
])
@pytest.mark.asyncio
async def test_only_explicit_passed_validation_is_resolved(storage, status, resolved, other):
    storage[0].documents = [qa(feedback=issue_feedback(status, classification=None))]
    total = (await QADashboardService().get_overview(start=START, end=END)).totals
    assert total.findings == 0  # A linked case does not require a classification.
    assert total.issue_cases == total.tracked_issues == 1
    assert total.resolved_cases == resolved
    skipped = int(status == "validation_skipped")
    assert total.resolved_rate.denominator == 1 - skipped
    assert total.resolved_rate.rate == (None if skipped else 100 * resolved)
    assert total.validation_skipped_cases == skipped
    assert total.unresolved_cases == 1 - resolved - skipped
    assert total.other_issue_cases == other


@pytest.mark.parametrize("statuses, resolved, skipped, unresolved", [
    ([], 0, 0, 0),
    (["validation_skipped", "validation_skipped"], 0, 2, 0),
    (["validation_passed", "validation_skipped"], 1, 1, 0),
    (["validation_passed"] * 6 + ["validation_skipped"] * 2 + ["pending_validation"] * 2, 6, 2, 2),
    (["validation_passed", "validation_skipped", "pending_validation", "validation_failed"], 1, 1, 2),
])
@pytest.mark.asyncio
async def test_overview_api_partitions_issue_cases_without_counting_skipped_as_resolved(
    storage, statuses, resolved, skipped, unresolved,
):
    import server

    storage[0].documents = [qa(feedback=issue_feedback(status)) for status in statuses]
    async with httpx.AsyncClient(transport=httpx.ASGITransport(app=server.app), base_url="http://test") as client:
        response = await client.get("/api/dashboard/overview", params={
            "start": START.isoformat(), "end": END.isoformat(),
        })
    assert response.status_code == 200
    data = response.json()
    for row in [*data["rows"], data["totals"]]:
        assert row["resolved_cases"] == resolved
        assert row["validation_skipped_cases"] == skipped
        assert row["unresolved_cases"] == unresolved
        assert row["issue_cases"] == resolved + skipped + unresolved
        eligible = len(statuses) - skipped
        assert row["resolved_rate"] == {
            "numerator": resolved, "denominator": eligible,
            "rate": 100 * resolved / eligible if eligible else None,
        }


@pytest.mark.parametrize("url", [
    None, "", "  ", 123, "not a url", "https://github.com/Azure/example/pull/1",
    "https://github.com.evil.test/Azure/example/issues/1", "https://github.com/Azure/example/issues/0",
    "https://github.com/Azure/example/issues/1/extra",
])
@pytest.mark.asyncio
async def test_missing_or_invalid_issue_links_never_inflate_resolution(storage, url):
    storage[0].documents = [qa(feedback={**issue_feedback(), "issue_url": url})]
    total = (await QADashboardService().get_overview(start=START, end=END)).totals
    assert total.findings == 1
    assert total.issue_cases == total.tracked_issues == total.resolved_cases == 0
    assert total.resolved_rate.rate is None


@pytest.mark.asyncio
async def test_issue_identity_normalization(storage):
    urls = [
        "https://github.com/Azure/example/issues/1",
        " https://GITHUB.COM/azure/Example/issues/01/ ",
        "https://github.com/Azure/example/issues/1#issuecomment-123",
        "https://github.com/Azure/example/issues/1?source=report",
        "https://github.com/Azure/other/issues/1",
    ]
    storage[0].documents = [qa(feedback={**issue_feedback(), "issue_url": url}) for url in urls]
    total = (await QADashboardService().get_overview(start=START, end=END)).totals
    assert total.tracked_issues == 2
    assert total.issue_cases == total.resolved_cases == 5


@pytest.mark.asyncio
async def test_findings_include_all_known_causes_without_inference(storage):
    storage[0].documents = [qa(verdict="incorrect", feedback={"classification": cause.value})
                            for cause in RootCauseClassification]
    storage[0].documents += [qa(feedback={"classification": value}) for value in (None, "", "new_cause")]
    total = (await QADashboardService().get_overview(start=START, end=END)).totals
    assert total.findings == len(RootCauseClassification)
    assert total.root_causes == {cause: 1 for cause in RootCauseClassification}
    assert sum(total.root_causes.values()) == total.findings
    assert total.accuracy_excluded == 4
    assert total.incorrect == 2  # Retrieval mismatch and reasoning gap remain eligible.
    assert total.accuracy.model_dump() == {"numerator": 3, "denominator": 5, "rate": 60}
    assert total.issue_cases == 0
    assert total.model_dump(mode="json")["root_causes"]["missing_content"] == 1


@pytest.mark.asyncio
async def test_issue_scope_uses_conversation_cohort_and_current_status(storage):
    storage[0].documents = [
        qa("a", feedback={**issue_feedback(), "validated_at": (END + timedelta(days=30)).isoformat()}),
        qa("b", feedback=issue_feedback(number=2)),
        qa("test", feedback=issue_feedback(number=3)),
        qa("a", conversation_created_at=(START - timedelta(seconds=1)).isoformat(), feedback=issue_feedback(number=4)),
        qa("a", conversation_created_at=END.isoformat(), feedback=issue_feedback(number=5)),
        qa("a", conversation_created_at=None, feedback=issue_feedback(number=6)),
    ]
    report = await QADashboardService().get_overview(start=START, end=END, channel_id="a")
    assert report.totals.findings == report.totals.tracked_issues == report.totals.resolved_cases == 1
    assert report.totals.resolved_rate.rate == 100
    assert report.totals.root_causes == {"reasoning_gap": 1}
    empty = await QADashboardService().get_overview(start=START, end=END, channel_id="test")
    assert empty.totals.tracked_issues == empty.totals.findings == 0
    assert empty.totals.resolved_rate.rate is None


def test_resolution_tables_keep_skipped_separate_from_unresolved():
    html = (Path(__file__).resolve().parent.parent / "static/qa_records_dashboard.html").read_text(encoding="utf-8")
    assert "data.totals.resolved_rate" in html
    assert "Issues includes all issue-linked cases, including skipped cases" not in html
    assert "row.resolved_rate.numerator} / ${row.resolved_rate.denominator}" in html
    assert "Resolved rate = resolved cases / (issues − skipped cases)" in html
    summary = html.split('title: "Issue findings",', 1)[1].split("function reportRowValues", 1)[0]
    assert "limitations:" not in summary
    assert "values: row => [row.issue_cases," in summary
    assert "row.issues" not in summary
    assert "row.findings" not in summary
    assert "row.tracked_issues" not in summary
    assert "row.resolved_cases, row.validation_skipped_cases," in summary
    assert "row.unresolved_cases" not in summary
    assert 'title: "Unresolved cases"' not in html
    assert 'title: "Root-cause findings"' not in html


@pytest.mark.asyncio
async def test_overview_route_validates_and_serializes(storage):
    import server

    storage[0].documents = [qa(feedback=issue_feedback())]
    async with httpx.AsyncClient(transport=httpx.ASGITransport(app=server.app), base_url="http://test") as client:
        response = await client.get("/api/dashboard/overview", params={
            "start": START.isoformat(), "end": END.isoformat(), "channel_id": "a",
        })
        assert response.status_code == 200
        assert response.json()["totals"]["accuracy"]["rate"] == 100
        assert response.json()["totals"]["resolved_rate"] == {"numerator": 1, "denominator": 1, "rate": 100}
        assert response.json()["totals"]["tracked_issues"] == 1
        assert response.json()["totals"]["root_causes"] == {"reasoning_gap": 1}
        assert "tenant_id" not in response.json()
        assert response.json()["channel_id"] == "a"
        assert "notes" not in response.json()
        for row in [*response.json()["rows"], response.json()["totals"]]:
            assert "tenant_id" not in row
            assert "undated_conversations" not in row
            for name in ("accuracy", "expert_interaction", "answer_rate", "resolved_rate"):
                assert "goal" not in row[name]
                assert set(row[name]) == {"numerator", "denominator", "rate"}
        for params in [
            {}, {"start": "invalid", "end": END.isoformat()},
            {"start": "2026-09-07", "end": END.isoformat()},
            {"start": END.isoformat(), "end": START.isoformat()},
            {"start": START.isoformat(), "end": (END + timedelta(days=100)).isoformat()},
            {"start": START.isoformat(), "end": END.isoformat(), "channel_id": "x" * 201},
        ]:
            invalid = await client.get("/api/dashboard/overview", params=params)
            assert invalid.status_code == 422


def test_overview_openapi_removes_tenant_only_from_overview():
    import server
    from models.qa_record import QARecord

    schema = server.app.openapi()
    assert "notes" not in schema["components"]["schemas"]["QAOverview"]["properties"]
    parameters = schema["paths"]["/api/dashboard/overview"]["get"]["parameters"]
    assert {parameter["name"] for parameter in parameters} == {"start", "end", "channel_id"}
    for name in ("QAOverview", "OverviewRow"):
        assert "tenant_id" not in schema["components"]["schemas"][name]["properties"]
        assert "undated_conversations" not in schema["components"]["schemas"][name]["properties"]
    assert "tenant_id" in QARecord.model_fields
    assert "tenant_id" in schema["components"]["schemas"]["QADashboardRecord"]["properties"]
    for endpoint in ("qa-records", "qa-record-details"):
        parameters = schema["paths"][f"/api/dashboard/{endpoint}"]["get"]["parameters"]
        tenant = next(parameter for parameter in parameters if parameter["name"] == "tenant_id")
        assert tenant["required"] is (endpoint == "qa-record-details")


def test_conversation_assessment_displays_expert_interaction_and_reason():
    html = (Path(__file__).resolve().parent.parent / "static/qa_records_dashboard.html").read_text(encoding="utf-8")
    assessment = html.split("function renderAssessment(record)", 1)[1].split("function renderDiagnosis", 1)[0]
    assert '"Expert interaction"' in assessment
    assert 'record.has_expert_interaction === true ? "Yes"' in assessment
    assert 'record.has_expert_interaction === false ? "No" : "Not assessed"' in assessment
    assert 'if (record.expert_interaction_reason)' in assessment
    assert 'evidence("Expert interaction reason", record.expert_interaction_reason)' in assessment
    assert "has_expert_reply" not in assessment
    assert "innerHTML" not in assessment


def test_overview_html_contract():
    html = (Path(__file__).resolve().parent.parent / "static/qa_records_dashboard.html").read_text(encoding="utf-8")
    assert "innerHTML" not in html
    for fragment in ("overview-tab", "conversations-tab", "report-start", "report-end", "Copy report", "Print report", "setDate", "buildOverviewReport", "reportRowValues", "AbortController", "overviewRequest !== request", "report.fallback.select()"):
        assert fragment in html
    assert 'id="report-end" type="date" required' in html
    assert 'aria-label="Weekly Status Report"' in html
    assert '"# Weekly Status Report"' in html
    assert "manager" not in html.lower()
    assert "report-tenant" not in html
    report_script = html.split("const report = {", 1)[1]
    assert "setUTCDate" not in report_script
    assert "T00:00:00Z" not in report_script
    assert "const defaultRange = lastSevenDays();" in report_script
    assert "start: start.toISOString(), end: end.toISOString()" in report_script
    assert "reportHeading" not in report_script
    assert "Generated:" not in report_script
    assert 'report.period.textContent = "";' in report_script
    assert "tenant" not in report_script.lower()
    assert "tenant_id: record.tenant_id" in html
    assert 'report.print.addEventListener("click", () => { if (overviewData) window.print(); });' in html


def test_dashboard_pages_use_distinct_routes_and_load_only_selected_data():
    html = (Path(__file__).resolve().parent.parent / "static/qa_records_dashboard.html").read_text(encoding="utf-8")
    assert '<a id="overview-tab" href="/dashboard/overview">Overview</a>' in html
    assert '<a id="conversations-tab" href="/dashboard/qa-records">Conversations</a>' in html
    assert r'window.location.pathname.replace(/\/$/, "") !== "/dashboard/qa-records"' in html
    assert 'selectView(isOverview);\n    if (isOverview) loadOverview();\n    else loadRecords();' in html
    assert 'link.setAttribute("aria-current", "page")' in html
    assert 'link.removeAttribute("aria-current")' in html
    assert 'document.querySelector("#overview-panel").hidden = !overview;' in html
    assert 'document.querySelector("#conversations-panel").hidden = overview;' in html
    assert 'conversationsLoaded' not in html
    assert 'addEventListener("click", () => selectView(' not in html


def test_overview_filters_reload_automatically_without_action_buttons():
    html = (Path(__file__).resolve().parent.parent / "static/qa_records_dashboard.html").read_text(encoding="utf-8")
    form = html.split('<form id="overview-filters"', 1)[1].split("</form>", 1)[0]
    assert "<button" not in form
    for removed in ("Update report", "Last full week", "last-week", "lastFullWeek", "setLastWeek"):
        assert removed not in html
    assert 'report.form.addEventListener("input", invalidateOverview)' in html
    assert 'report.form.addEventListener("change", loadOverview)' in html
    assert 'event.preventDefault(); loadOverview();' in html
    loading = html.split("async function loadOverview()", 1)[1].split('report.form.addEventListener', 1)[0]
    assert loading.index("invalidateOverview();") < loading.index("const start =")
    assert loading.index("end <= start") < loading.index("await fetch(")


def test_overview_date_selection_includes_end_day_and_defaults_to_seven_complete_days():
    html = (Path(__file__).resolve().parent.parent / "static/qa_records_dashboard.html").read_text(encoding="utf-8")
    assert 'To (local time, inclusive)<input id="report-end"' in html
    assert "but not the end date" not in html
    default = html.split("function lastSevenDays(", 1)[1].split("function percent", 1)[0]
    assert "start.setDate(start.getDate() - 7)" in default
    assert "end.setDate(end.getDate() - 1)" in default
    loading = html.split("async function loadOverview()", 1)[1].split('report.form.addEventListener', 1)[0]
    assert loading.index("end.setDate(end.getDate() + 1)") < loading.index("end <= start")
    assert "end.getTime() + 86400000" not in loading
    assert "From on or before To" in loading


def test_overview_tables_have_no_goal_columns_or_threshold_titles():
    html = (Path(__file__).resolve().parent.parent / "static/qa_records_dashboard.html").read_text(encoding="utf-8")
    # Scope to overview presentation; conversation links legitimately use target.
    tables = html.split("const reportTables = [", 1)[1].split("function reportRowValues", 1)[0]
    for removed in ("goal", "target", "threshold", "not met", "incomplete"):
        assert removed not in tables.lower()
    assert re.findall(r'title: "([^"]+)"', tables) == [
        "Accuracy", "Interaction rate", "Answer rate",
        "Issue findings",
    ]
    headings = [json.loads(value) for value in re.findall(r"headings: (\[[^\n]+\])", tables)]
    assert headings == [
        ["Channel", "Conversations", "Incorrect", "Excluded", "Accuracy"],
        ["Channel", "Conversations", "Expert interactions", "Interaction rate"],
        ["Channel", "In-scope questions", "Answered questions", "Answer rate"],
        ["Channel", "Issues", "Resolved", "Skipped", "Resolved rate"],
    ]
    for removed in ("undated_conversations", ".coverage"):
        assert removed not in tables
    accuracy_table = tables.split('title: "Interaction rate",', 1)[0]
    assert "values: row => [row.conversations, row.incorrect, row.accuracy_excluded," in accuracy_table
    assert "row.correct" not in accuracy_table
    assert "${percent(row.accuracy.rate)} (${row.accuracy.numerator} / ${row.accuracy.denominator})" in accuracy_table
    assert tables.count("description:") == 4
    assert tables.count("limitations:") == 3
    assert "Missing, outdated, or insufficient documentation and out-of-scope cases are excluded" in tables
    assert "Accuracy = correct answered conversations / (conversations − excluded)." in tables
    # Both visible/printed tables and the copied Markdown use these definitions.
    rendering = html.split("function renderOverview(data)", 1)[1].split("function markdownValue", 1)[0]
    copying = html.split("function buildOverviewReport(data)", 1)[1].split("function invalidateOverview", 1)[0]
    for consumer in (rendering, copying):
        assert "for (const definition of reportTables)" in consumer
        assert "reportRowValues(definition," in consumer
        assert "definition.description" in consumer
        assert "definition.limitations" in consumer


def test_interaction_rate_table_shows_percentage_and_counts():
    html = (Path(__file__).resolve().parent.parent / "static/qa_records_dashboard.html").read_text(encoding="utf-8")
    interaction = html.split('title: "Interaction rate",', 1)[1].split('title: "Answer rate",', 1)[0]
    assert "values: row => [row.conversations, row.expert_yes," in interaction
    assert "${percent(row.expert_interaction.rate)} (${row.expert_interaction.numerator} / ${row.expert_interaction.denominator})" in interaction


def test_answer_rate_table_shows_percentage_and_counts():
    html = (Path(__file__).resolve().parent.parent / "static/qa_records_dashboard.html").read_text(encoding="utf-8")
    answer = html.split('title: "Answer rate",', 1)[1].split('title: "Issue findings",', 1)[0]
    assert "values: row => [row.questions, row.answered_questions," in answer
    assert "Answer rate = answered in-scope questions / all in-scope questions." in answer
    assert "row.bot_replies" not in answer
    assert "${percent(row.answer_rate.rate)} (${row.answer_rate.numerator} / ${row.answer_rate.denominator})" in answer


def test_overview_visual_shows_total_rates_with_inline_counts_without_bars():
    html = (Path(__file__).resolve().parent.parent / "static/qa_records_dashboard.html").read_text(encoding="utf-8")
    visual = html.split("function renderOverviewVisual(data)", 1)[1].split("function renderOverview(data)", 1)[0]
    for field in ("accuracy", "expert_interaction", "answer_rate", "resolved_rate"):
        assert f"data.totals.{field}" in visual
    assert "At a glance" not in html
    assert "Channels with data" not in html
    assert "overview-count" not in html
    assert "overview-caption" not in html
    assert 'section.setAttribute("aria-label", "Total data overview")' in visual
    assert "percent(metric.rate)" in visual
    assert "(${metric.numerator.toLocaleString()} / ${metric.denominator.toLocaleString()})" in visual
    assert "rate.append(rateCounts)" in visual
    assert 'rateCounts.title = `${labels[0]} / ${labels[1]}`' in visual
    assert 'rateCounts.setAttribute("aria-label"' in visual
    assert "overview-bar" not in html
    assert "Accuracy = correct answered conversations / (conversations − excluded)." in html
    assert "report.tables.replaceChildren(renderOverviewVisual(data))" in html
    invalidation = html.split("function invalidateOverview()", 1)[1].split("async function loadOverview()", 1)[0]
    assert "report.tables.replaceChildren();" in invalidation
    assert ".overview-metrics { grid-template-columns: 1fr; }" in html
    assert "print-color-adjust: exact" in html
    assert ".report-section table { min-width: 0; table-layout: fixed; }" in html
    assert ".report-section th, .report-section td { overflow-wrap: anywhere; }" in html


def test_metric_descriptions_keep_cards_short_and_define_counts_in_notes():
    html = (Path(__file__).resolve().parent.parent / "static/qa_records_dashboard.html").read_text(encoding="utf-8")
    visual = html.split("function renderOverviewVisual(data)", 1)[1].split("function renderOverview(data)", 1)[0]
    notes = re.findall(r'note: "([^"]+)"', visual)
    assert len(notes) == 4
    assert all(len(note.split()) <= 15 for note in notes)
    tables = html.split("const reportTables = [", 1)[1].split("function reportRowValues", 1)[0]
    descriptions = re.findall(r'description: "([^"]+)"', tables)
    limitations = [json.loads(value) for value in re.findall(r"limitations: (\[[^\n]+\])", tables)]
    assert len(descriptions) == 4
    assert len(limitations) == 3
    assert all(len(description.split()) <= 20 for description in descriptions)
    assert all(len(items) <= 1 for items in limitations)
    assert all(len(note.split()) <= 25 for items in limitations for note in items)
    accuracy, interaction, answer, resolution = notes
    assert all(" / " in note for note in (accuracy, answer, resolution))
    assert "All conversations excluding documentation issues and out-of-scope cases" in accuracy
    assert "Missing, outdated, or insufficient documentation and out-of-scope cases are excluded" in html
    assert "expert follow-up after a bot reply" in interaction
    assert "adds guidance beyond the bot's answer" in html
    assert "answered in-scope questions / all in-scope questions" in answer.lower()
    assert "Scope: needs reply or mentions bot" in html
    assert "inferred once" in html
    assert "resolved cases / (issues − skipped cases)" in tables
    assert "skipped" in resolution.lower()
    assert "Issues includes all issue-linked cases, including skipped cases" not in html
    for misleading in ("count as successful", "excluded conversations as successful", "terminal"):
        assert misleading not in visual


def test_overview_keeps_collapsible_notes_in_display_copy_and_print_without_document_dependency():
    html = (Path(__file__).resolve().parent.parent / "static/qa_records_dashboard.html").read_text(encoding="utf-8")
    for removed in ("conversationLimitations", "sharedLimitations", "resolutionLimitations"):
        assert removed not in html
    assert "metric-definitions" not in html
    assert 'node("details", "report-notes")' in html
    assert 'node("summary", "", "Notes")' in html
    assert "metric-help" not in html
    rendering = html.split("function renderOverview(data)", 1)[1].split("function markdownValue", 1)[0]
    assert "definition.limitations.map(note => node(\"li\", \"\", note))" in rendering
    assert "if (definition.limitations?.length)" in rendering
    copying = html.split("function buildOverviewReport(data)", 1)[1].split("function invalidateOverview", 1)[0]
    assert 'const lines = ["# Weekly Status Report", ""];' in copying
    assert "### Notes" in copying
    assert "definition.limitations" in copying
    assert "(definition.limitations ?? []).map" in copying
    assert 'window.addEventListener("beforeprint"' in html
    assert "notes => ({notes, open: notes.open})" in html
    assert "notes.open = true" in html
    assert 'window.addEventListener("afterprint"' in html
    assert "notes.open = open" in html


def test_overview_channel_names_link_to_teams_without_displaying_ids():
    html = (Path(__file__).resolve().parent.parent / "static/qa_records_dashboard.html").read_text(encoding="utf-8")
    assert '<label>Channel<select id="report-channel"><option value="">All channels</option></select></label>' in html
    assert "Channel ID (optional)" not in html
    assert 'id="report-channels"' not in html
    assert "const choices = report.channel;" in html
    assert 'node("option", "", row.channel_name)' in html
    assert "option.value = row.channel_id;" in html
    values = html.split("function reportRowValues", 1)[1].split("function teamsChannelUrl", 1)[0]
    assert "row.channel_id" not in values
    assert 'totalRow ? "Total" : row.channel_name' in values
    assert "https://teams.microsoft.com/l/channel/${encodeURIComponent(row.channel_id)}" in html
    channel_cell = html.split("function reportChannelCell", 1)[1].split("function renderOverview", 1)[0]
    assert 'node("a", "", row.channel_name)' in channel_cell
    assert 'link.rel = "noopener noreferrer"' in channel_cell
    assert "if (url)" in channel_cell
    assert "tr.append(reportChannelCell(row)" in html
    assert "const url = totalRow ? null : teamsChannelUrl(row);" in html
    assert "if (url) values[0] =" in html


@pytest.mark.parametrize("flag", [True, False, None, "missing"])
@pytest.mark.parametrize("name", ["Azure SDK Q&A Bot", "Azure SDK Q&amp;A Bot", "azure sdk q&amp;a bot"])
@pytest.mark.asyncio
async def test_explicit_bot_mentions_count_once_regardless_of_reply_flag(storage, flag, name):
    _, messages = storage
    question = message(should_reply=flag, content=f'<at id="0">{name}</at> help please')
    if flag == "missing":
        question.pop("should_reply")
    messages.documents = [question, message(role="system")]
    result = await QADashboardService().get_overview(start=START, end=END)
    assert result.totals.questions == 1
    assert result.totals.answered_questions == 1
    assert result.totals.answer_rate.rate == 100
    assert result.totals.answer_rate.denominator == 1
    assert "c.content" in messages.calls[0]["query"]


@pytest.mark.parametrize("content", [
    None, "", 123, "Azure SDK Q&A Bot please help", "@Azure SDK Q&A Bot",
    '<at id="0">Other Bot</at>', '<at id="0">Azure SDK Q&A Bot Extra</at>',
])
@pytest.mark.asyncio
async def test_non_mentions_without_reply_flag_do_not_create_scope_rows(storage, content):
    storage[1].documents = [message(should_reply=None, content=content)]
    result = await QADashboardService().get_overview(start=START, end=END)
    assert result.rows == []
    assert result.totals.questions == 0
    assert result.totals.answer_rate.rate is None


@pytest.mark.asyncio
async def test_only_users_are_questions_and_mention_dates_remain_half_open(storage):
    mention = '<at id="0">Azure SDK Q&amp;A Bot</at>'
    storage[1].documents = [
        message(should_reply=False, content=mention),
        message(should_reply=False, content=mention, created_at=END.isoformat()),
        message(role="system", content=mention),
        message(role="assistant", content=mention),
        message(role="developer", content=mention),
    ]
    result = await QADashboardService().get_overview(start=START, end=END)
    assert result.totals.questions == 1
    assert result.totals.answered_questions == 1