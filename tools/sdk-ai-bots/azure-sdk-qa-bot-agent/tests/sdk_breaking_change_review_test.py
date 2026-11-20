"""Regression tests for live spec PR review ordering."""

from unittest.mock import AsyncMock, MagicMock

import httpx
import pytest

from models.chat import ChatRequest
from services import sdk_breaking_change_review as review
from services.chat_service import ChatService
from tools import github_mcp_tools
from utils.background_tasks import BackgroundTaskTracker


_PR = "https://github.com/Azure/azure-rest-api-specs/pull/42"
_QUESTION = "Is BreakingChange-Go-Sdk a valid concern for this refactor?"


def _request(content, **kwargs):
    return ChatRequest(
        tenant_id="general_qa_bot",
        message={"role": "user", "content": content},
        **kwargs,
    )


@pytest.mark.asyncio
@pytest.mark.parametrize(
    "labels, expected",
    [
        ({"resource-manager", "waitforarmfeedback"}, "Complete ARM review"),
        ({"breakingchange-go-sdk"}, "Complete ARM review"),
        ({"armsignedoff", "breakingchangereviewrequired"}, "BreakingChangeReviewRequired"),
        ({"armreview", "versioningreviewrequired"}, "VersioningReviewRequired"),
        ({"armsignedoff", "notreadyforarmreview"}, "Resolve the ARM review blockers"),
        ({"resource-manager", "breakingchangereviewrequired", "breakingchange-approved-benign"},
         "Complete ARM review"),
        ({"armsignedoff", "breakingchangereviewrequired", "breakingchange-approved-benign",
          "versioningreviewrequired", "versioning-approved-bugfix"}, None),
        ({"data-plane"}, None),
    ],
)
async def test_live_pr_gate_order(monkeypatch, labels, expected):
    fetch = AsyncMock(return_value=labels)
    monkeypatch.setattr(review, "get_spec_pull_request_labels", fetch)
    answer = await review.sdk_breaking_change_review_hold(
        _request(f'{_QUESTION} <a href="{_PR}/files">PR</a>')
    )

    fetch.assert_awaited_once_with("azure-rest-api-specs", 42)
    if expected is None:
        assert answer is None
    else:
        assert "SDK breaking-change approval is premature" in answer
        assert expected in answer
        if expected.endswith("ReviewRequired"):
            assert answer.index(expected) < answer.index("Then confirm ARM signoff")


@pytest.mark.asyncio
@pytest.mark.parametrize(
    "question",
    [f"Can you summarize {_PR}?", _QUESTION, f"{_QUESTION} https://github.com/Azure/azure-sdk-for-go/pull/42"],
)
async def test_unrelated_or_unlinked_questions_do_not_query_github(monkeypatch, question):
    fetch = AsyncMock()
    monkeypatch.setattr(review, "get_spec_pull_request_labels", fetch)

    assert await review.sdk_breaking_change_review_hold(_request(question)) is None
    fetch.assert_not_awaited()


@pytest.mark.asyncio
async def test_unavailable_private_pr_never_implies_approval(monkeypatch):
    fetch = AsyncMock(side_effect=TimeoutError)
    monkeypatch.setattr(review, "get_spec_pull_request_labels", fetch)
    req = _request(
        "Can I request SDK breaking-change approval?",
        additional_infos=[{
            "type": "link", "content": "PR",
            "link": "https://github.com/Azure/azure-rest-api-specs-pr/pull/42",
        }],
    )

    answer = await review.sdk_breaking_change_review_hold(req)

    fetch.assert_awaited_once_with("azure-rest-api-specs-pr", 42)
    assert "couldn't verify the current review gates" in answer
    assert "Next Steps to Merge" in answer


@pytest.mark.asyncio
async def test_completion_cannot_emit_sdk_advice_while_arm_signoff_is_pending(monkeypatch):
    import server

    def github_response(request):
        assert request.url == "https://api.github.com/repos/Azure/azure-rest-api-specs/pulls/42"
        return httpx.Response(200, json={
            "labels": [{"name": "ARMReview"}, {"name": "BreakingChange-Go-Sdk"}],
        })

    github_client = httpx.AsyncClient(transport=httpx.MockTransport(github_response))
    project = MagicMock()
    service = ChatService(project_client=project, settings=lambda key, default=None: default)
    invoked = AsyncMock(side_effect=AssertionError("Must not ask the hosted agent"))
    monkeypatch.setattr(service, "_get_agent", invoked)
    monkeypatch.setattr(server, "_chat_service", service)
    monkeypatch.setattr(github_mcp_tools, "_get_github_token", AsyncMock(return_value=("test-token", None)))

    async with httpx.AsyncClient(
        transport=httpx.ASGITransport(app=server.app), base_url="http://test",
    ) as client:
        monkeypatch.setattr(github_mcp_tools.httpx, "AsyncClient", lambda **kwargs: github_client)
        response = await client.post(
            "/completion",
            json=_request(f"{_QUESTION} {_PR}").model_dump(mode="json"),
        )
    await BackgroundTaskTracker.instance().shutdown()

    assert response.status_code == 200
    assert response.json()["has_result"] is True
    answer = response.json()["answer"]
    assert "ARM signoff (`ARMSignedOff`) is not present" in answer
    assert "suppression" not in answer
    invoked.assert_not_awaited()
