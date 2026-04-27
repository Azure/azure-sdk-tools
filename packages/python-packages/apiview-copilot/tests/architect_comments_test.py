# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""Tests for get_architect_comments in cli.py."""

import json

import cli


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

class MockContainerClient:
    """Minimal mock for Cosmos container query_items."""

    def __init__(self, items):
        self.items = items

    def query_items(self, **_kwargs):  # pylint: disable=unused-argument
        return iter(self.items)


def _make_comment(
    id: str,
    review_id: str = "rev1",
    thread_id: str = "thread1",
    created_by: str = "architect1",
    created_on: str = "2026-04-10T00:00:00Z",
    comment_source: str = "UserGenerated",
    severity: str = "ShouldFix",
    comment_text: str = "Fix this.",
    element_id: str = "SomeClass.method",
) -> dict:
    return {
        "id": id,
        "ReviewId": review_id,
        "APIRevisionId": "apirev1",
        "ElementId": element_id,
        "CommentText": comment_text,
        "CreatedBy": created_by,
        "CreatedOn": created_on,
        "IsResolved": False,
        "Upvotes": [],
        "Downvotes": [],
        "CommentType": "APIRevision",
        "CommentSource": comment_source,
        "IsDeleted": False,
        "ThreadId": thread_id,
        "Severity": severity,
    }


def _patch_common(monkeypatch, raw_comments, approvers=None, review_lang_items=None, thread_starts=None):
    """Apply common monkeypatches for get_architect_comments tests."""
    monkeypatch.setattr(cli, "get_comments_in_date_range", lambda *a, **kw: raw_comments)
    monkeypatch.setattr(cli, "get_approvers", lambda **kw: approvers or set())
    monkeypatch.setattr(
        cli,
        "get_apiview_cosmos_client",
        lambda **kw: MockContainerClient(review_lang_items or []),
    )
    monkeypatch.setattr(
        cli,
        "get_thread_start_dates",
        lambda filtered, environment=None: thread_starts or {},
    )
    # Use real to_iso8601 and get_language_pretty_name — they are pure functions.


# ---------------------------------------------------------------------------
# Tests — approver filtering
# ---------------------------------------------------------------------------


def test_filters_to_approvers_only(monkeypatch, capsys):
    """Only comments from approved architects should appear in default mode."""
    comments = [
        _make_comment("c1", created_by="architect1"),
        _make_comment("c2", created_by="random_user", thread_id="thread2"),
    ]
    _patch_common(
        monkeypatch,
        raw_comments=comments,
        approvers={"architect1"},
        review_lang_items=[{"id": "rev1", "Language": "Python"}],
        thread_starts={"thread1": "2026-04-10T00:00:00Z", "thread2": "2026-04-10T00:00:00Z"},
    )

    cli.get_architect_comments(start_date="2026-04-01", end_date="2026-04-30")
    output = json.loads(capsys.readouterr().out)

    assert len(output) == 1
    assert output[0]["CreatedBy"] == "architect1"


def test_all_commenters_skips_approver_filter(monkeypatch, capsys):
    """With all_commenters=True, comments from any user should appear."""
    comments = [
        _make_comment("c1", created_by="architect1"),
        _make_comment("c2", created_by="random_user", thread_id="thread2"),
    ]
    _patch_common(
        monkeypatch,
        raw_comments=comments,
        approvers={"architect1"},
        review_lang_items=[{"id": "rev1", "Language": "Python"}],
        thread_starts={"thread1": "2026-04-10T00:00:00Z", "thread2": "2026-04-10T00:00:00Z"},
    )

    cli.get_architect_comments(start_date="2026-04-01", end_date="2026-04-30", all_commenters=True)
    output = json.loads(capsys.readouterr().out)

    assert len(output) == 2
    authors = {c["CreatedBy"] for c in output}
    assert authors == {"architect1", "random_user"}


def test_excludes_ai_generated_comments(monkeypatch, capsys):
    """AI-generated comments should always be excluded."""
    comments = [
        _make_comment("c1", created_by="architect1", comment_source="UserGenerated"),
        _make_comment("c2", created_by="architect1", comment_source="AIGenerated", thread_id="thread2"),
    ]
    _patch_common(
        monkeypatch,
        raw_comments=comments,
        approvers={"architect1"},
        review_lang_items=[{"id": "rev1", "Language": "Python"}],
        thread_starts={"thread1": "2026-04-10T00:00:00Z"},
    )

    cli.get_architect_comments(start_date="2026-04-01", end_date="2026-04-30")
    output = json.loads(capsys.readouterr().out)

    assert len(output) == 1
    assert output[0]["id"] == "c1"


# ---------------------------------------------------------------------------
# Tests — include_replies semantics
# ---------------------------------------------------------------------------


def test_include_replies_returns_non_approver_replies_in_approver_threads(monkeypatch, capsys):
    """When include_replies=True, all comments in threads started by an approver
    should be returned, even if some replies are from non-approvers."""
    comments = [
        # Thread started by architect
        _make_comment(
            "c1", thread_id="thread1", created_by="architect1",
            created_on="2026-04-10T00:00:00Z", severity="ShouldFix",
        ),
        # Reply from non-approver in same thread
        _make_comment(
            "c2", thread_id="thread1", created_by="sdk_dev",
            created_on="2026-04-10T01:00:00Z", severity=None,
            comment_text="Got it, will fix.",
        ),
        # Thread started by non-approver — should be excluded
        _make_comment(
            "c3", thread_id="thread2", created_by="sdk_dev",
            created_on="2026-04-10T02:00:00Z",
        ),
    ]
    _patch_common(
        monkeypatch,
        raw_comments=comments,
        approvers={"architect1"},
        review_lang_items=[{"id": "rev1", "Language": "Python"}],
        thread_starts={"thread1": "2026-04-10T00:00:00Z", "thread2": "2026-04-10T02:00:00Z"},
    )

    cli.get_architect_comments(
        start_date="2026-04-01", end_date="2026-04-30", include_replies=True,
    )
    output = json.loads(capsys.readouterr().out)

    ids = {c["id"] for c in output}
    assert ids == {"c1", "c2"}, "Should include approver comment AND non-approver reply"
    assert "c3" not in ids, "Thread started by non-approver should be excluded"


def test_include_replies_without_approver_filter_returns_all(monkeypatch, capsys):
    """When include_replies=True and all_commenters=True, all comments are returned."""
    comments = [
        _make_comment("c1", thread_id="thread1", created_by="architect1"),
        _make_comment("c2", thread_id="thread1", created_by="sdk_dev"),
        _make_comment("c3", thread_id="thread2", created_by="sdk_dev"),
    ]
    _patch_common(
        monkeypatch,
        raw_comments=comments,
        approvers={"architect1"},
        review_lang_items=[{"id": "rev1", "Language": "Python"}],
        thread_starts={"thread1": "2026-04-10T00:00:00Z", "thread2": "2026-04-10T00:00:00Z"},
    )

    cli.get_architect_comments(
        start_date="2026-04-01", end_date="2026-04-30",
        include_replies=True, all_commenters=True,
    )
    output = json.loads(capsys.readouterr().out)

    assert len(output) == 3


# ---------------------------------------------------------------------------
# Tests — per-thread selection (default, no replies)
# ---------------------------------------------------------------------------


def test_default_keeps_only_first_comment_per_thread(monkeypatch, capsys):
    """Without include_replies, only the earliest comment per thread is returned."""
    comments = [
        _make_comment(
            "c1", thread_id="thread1", created_by="architect1",
            created_on="2026-04-10T00:00:00Z",
        ),
        _make_comment(
            "c2", thread_id="thread1", created_by="architect1",
            created_on="2026-04-10T01:00:00Z",
        ),
    ]
    _patch_common(
        monkeypatch,
        raw_comments=comments,
        approvers={"architect1"},
        review_lang_items=[{"id": "rev1", "Language": "Python"}],
        thread_starts={"thread1": "2026-04-10T00:00:00Z"},
    )

    cli.get_architect_comments(start_date="2026-04-01", end_date="2026-04-30")
    output = json.loads(capsys.readouterr().out)

    assert len(output) == 1
    assert output[0]["id"] == "c1"


def test_default_excludes_threads_not_started_in_window(monkeypatch, capsys):
    """Threads that started before the date window should be excluded in default mode."""
    comments = [
        _make_comment(
            "c1", thread_id="thread1", created_by="architect1",
            created_on="2026-04-10T00:00:00Z",
        ),
        _make_comment(
            "c2", thread_id="thread_old", created_by="architect1",
            created_on="2026-04-11T00:00:00Z",
        ),
    ]
    _patch_common(
        monkeypatch,
        raw_comments=comments,
        approvers={"architect1"},
        review_lang_items=[{"id": "rev1", "Language": "Python"}],
        # thread_old started in March, outside the April window
        thread_starts={
            "thread1": "2026-04-10T00:00:00Z",
            "thread_old": "2026-03-15T00:00:00Z",
        },
    )

    cli.get_architect_comments(start_date="2026-04-01", end_date="2026-04-30")
    output = json.loads(capsys.readouterr().out)

    assert len(output) == 1
    assert output[0]["id"] == "c1"


def test_empty_approvers_returns_no_comments(monkeypatch, capsys):
    """When get_approvers() returns an empty set, no comments should be returned."""
    comments = [
        _make_comment("c1", created_by="some_user"),
        _make_comment("c2", created_by="another_user", thread_id="thread2"),
    ]
    _patch_common(
        monkeypatch,
        raw_comments=comments,
        approvers=set(),
        review_lang_items=[{"id": "rev1", "Language": "Python"}],
        thread_starts={"thread1": "2026-04-10T00:00:00Z", "thread2": "2026-04-10T00:00:00Z"},
    )

    cli.get_architect_comments(start_date="2026-04-01", end_date="2026-04-30")
    output = json.loads(capsys.readouterr().out)

    assert output == [], "Empty approvers set should produce no output, not fall back to all commenters"
