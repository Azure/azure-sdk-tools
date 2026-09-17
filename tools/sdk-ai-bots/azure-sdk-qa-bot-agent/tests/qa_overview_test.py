"""Offline report tests: real aggregation over async, query-aware fake containers."""

from datetime import datetime, timedelta, timezone
import json
from pathlib import Path
import re
from unittest.mock import AsyncMock

import httpx
import pytest

from models.qa_dashboard import OverviewCounts, OverviewRow
from services.qa_dashboard_service import QADashboardService
from services.qa_overview import REPORT_NOTES, validate_report_window

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
        for doc in self.documents:
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
        "created_at": START.isoformat(), "sender_role": role,
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
    messages.documents = [message() for _ in range(100)] + [message(role="assistant") for _ in range(98)]
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
    assert total.accuracy_excluded == 2
    assert total.correct == 1 and total.incorrect == 1
    assert total.accuracy.numerator == 5 and total.accuracy.denominator == 6
    assert total.accuracy.rate == pytest.approx(500 / 6)
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
async def test_questions_count_without_threads_and_replies_are_not_paired(storage):
    records, messages = storage
    messages.documents = [
        message("unanswered"), message("unanswered"), message("unanswered", should_reply=False),
        message("a", role="assistant"), message("a", role="system"),
        message("a", role="developer"), message("a", document_type="conversation_mapping"),
        message("a"), message("a", role="assistant"),
    ]
    result = await QADashboardService().get_overview(start=START, end=END)
    assert result.totals.conversations == 0
    unanswered = next(row for row in result.rows if row.channel_id == "unanswered")
    assert unanswered.questions == 2 and unanswered.answer_rate.rate == 0
    answered = next(row for row in result.rows if row.channel_id == "a")
    assert answered.bot_replies == 3 and answered.answer_rate.rate == 300
    assert result.totals.answer_rate.rate == 100
    assert "No question-to-reply pairing" in " ".join(result.notes)


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
    messages.documents = [message(extra_info=None)]
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
    for metric in (result.totals.accuracy, result.totals.expert_interaction, result.totals.answer_rate):
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


@pytest.mark.parametrize("changes, numerator, rate", [
    ({}, 0, None),
    ({"conversations": 10}, 10, 100),
    ({"conversations": 10, "incorrect": 2}, 8, 80),
    ({"conversations": 10, "incorrect": 10}, 0, 0),
    ({"conversations": 10, "accuracy_excluded": 10}, 10, 100),
])
def test_accuracy_subtracts_only_incorrect_from_all_conversations(changes, numerator, rate):
    row = OverviewRow(channel_name="Test", **changes)
    assert row.accuracy.model_dump() == {
        "numerator": numerator, "denominator": row.conversations, "rate": rate,
    }


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
    {"conversations": 10, "correct": 9, "incorrect": 1, "expert_yes": 1, "questions": 100, "bot_replies": 98},
    {"conversations": 1},
    {"conversations": 1, "correct": 1, "questions": 1, "bot_replies": 3},
])
def test_serialized_metrics_have_no_goal(changes):
    data = OverviewRow(channel_name="Test", **changes).model_dump(mode="json")
    assert "undated_conversations" not in OverviewCounts.model_fields
    assert "undated_conversations" not in OverviewRow.model_fields
    assert "undated_conversations" not in data
    for removed in ("correctness_unknown", "expert_no", "expert_unknown"):
        assert removed not in OverviewCounts.model_fields
        assert removed not in data
    for name in ("accuracy", "expert_interaction", "answer_rate"):
        assert "goal" not in data[name]
        assert set(data[name]) == {"numerator", "denominator", "rate"}


def test_report_notes_describe_metrics_without_goals():
    notes = " ".join(REPORT_NOTES).lower()
    for removed in ("goal", "target", "threshold", "marked met"):
        assert removed not in notes
    assert "tenant" not in notes
    assert "undated" not in notes
    assert "only dated conversations in the requested utc range [start, end)" in notes
    assert "coverage" not in notes
    assert "(conversations - incorrect) / conversations" in notes
    assert "unassessed and excluded conversations count as successful" in notes
    for retained in ("excluded", "totals", "zero denominators"):
        assert retained in notes


def test_report_notes_explain_persisted_expert_values_without_inferring_no_interaction():
    notes = " ".join(REPORT_NOTES).lower()
    for retained in (
        "expert_yes / conversations", "persisted has_expert_interaction values",
        "all conversations remain in the denominator, including null/missing assessments",
        "true means qualifying expert interaction, false means none, and null means insufficient evidence",
        "has_expert_reply is not used",
    ):
        assert retained in notes


def test_expert_instruction_and_example_match_boolean_metric_semantics():
    instruction = (Path(__file__).resolve().parent.parent / "agents/chatbot_evolution_agent/instruction.md").read_text(encoding="utf-8")
    assert "`false` if the complete transcript shows none; `null` if identity, ordering," in instruction
    example = json.loads(re.search(r"```json\s*(.*?)\s*```", instruction, re.DOTALL).group(1))
    assert example["has_expert_interaction"] is None
    assert "Insufficient evidence" in example["expert_interaction_reason"]


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


@pytest.mark.asyncio
async def test_overview_route_validates_and_serializes(storage):
    import server

    storage[0].documents = [qa()]
    async with httpx.AsyncClient(transport=httpx.ASGITransport(app=server.app), base_url="http://test") as client:
        response = await client.get("/api/dashboard/overview", params={
            "start": START.isoformat(), "end": END.isoformat(), "channel_id": "a",
        })
        assert response.status_code == 200
        assert response.json()["totals"]["accuracy"]["rate"] == 100
        assert "tenant_id" not in response.json()
        assert response.json()["channel_id"] == "a"
        assert response.json()["notes"]
        for row in [*response.json()["rows"], response.json()["totals"]]:
            assert "tenant_id" not in row
            assert "undated_conversations" not in row
            for name in ("accuracy", "expert_interaction", "answer_rate"):
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
    for fragment in ("overview-tab", "conversations-tab", "report-start", "report-end", "Last full week", "Copy report", "Print report", "setDate", "getDay", "buildOverviewReport", "reportRowValues", "report-notes", "AbortController", "overviewRequest !== request", "report.fallback.select()"):
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
    assert "Local time (${timezone})" in report_script
    assert "tenant" not in report_script.lower()
    assert "tenant_id: record.tenant_id" in html
    assert 'report.print.addEventListener("click", () => { if (overviewData) window.print(); });' in html


def test_overview_tables_have_no_goal_columns_or_threshold_titles():
    html = (Path(__file__).resolve().parent.parent / "static/qa_records_dashboard.html").read_text(encoding="utf-8")
    # Scope to overview presentation; conversation links legitimately use target.
    tables = html.split("const reportTables = [", 1)[1].split("function reportRowValues", 1)[0]
    for removed in ("goal", "target", "threshold", "not met", "incomplete"):
        assert removed not in tables.lower()
    assert re.findall(r'title: "([^"]+)"', tables) == [
        "Accuracy", "Interaction rate", "Answer rate",
    ]
    headings = [json.loads(value) for value in re.findall(r"headings: (\[[^\n]+\])", tables)]
    assert headings == [
        ["Channel", "Conversations", "Correct", "Excluded", "Accuracy"],
        ["Channel", "Conversations", "Expert interactions", "Interaction rate"],
        ["Channel", "In-scope questions", "Bot replies", "Answer rate"],
    ]
    for removed in ("undated_conversations", ".coverage"):
        assert removed not in tables
    assert "(Conversations - incorrect) / conversations" in tables
    assert "Unassessed and excluded conversations count as successful" in tables
    assert "Unassessed cases remain in the denominator" in tables
    assert len(re.findall(r'description: "[^"]+"', tables)) == 3
    # Both visible/printed tables and the copied Markdown use these definitions.
    rendering = html.split("function renderOverview(data)", 1)[1].split("function markdownValue", 1)[0]
    copying = html.split("function buildOverviewReport(data)", 1)[1].split("function invalidateOverview", 1)[0]
    for consumer in (rendering, copying):
        assert "for (const definition of reportTables)" in consumer
        assert "reportRowValues(definition," in consumer
        assert "definition.description" in consumer


def test_overview_notes_are_collapsed_and_metric_help_supports_hover_and_focus():
    html = (Path(__file__).resolve().parent.parent / "static/qa_records_dashboard.html").read_text(encoding="utf-8")
    notes = next((match for match in re.finditer(r"<details\b([^>]*)>(.*?)</details>", html, re.DOTALL)
                  if 'id="report-notes"' in match.group(2)), None)
    assert notes is not None, "Report notes must be inside native details"
    assert not re.search(r"\bopen\b", notes.group(1))
    assert "<summary" in notes.group(2)
    rendering = html.split("function renderOverview(data)", 1)[1].split("function markdownValue", 1)[0]
    assert re.search(r'node\("details",\s*"metric-help"\)', rendering)
    assert 'node("summary"' in rendering
    assert "definition.title" in rendering and "definition.description" in rendering
    assert ".metric-help:hover" in html
    assert ".metric-help:focus-within" in html


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
    assert result.totals.bot_replies == 1
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
    assert result.totals.bot_replies == 2