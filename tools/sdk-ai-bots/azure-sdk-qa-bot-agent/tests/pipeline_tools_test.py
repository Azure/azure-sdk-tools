"""Unit tests for pipeline_tools and utils.ado_token."""

from __future__ import annotations

import base64
import json
import sys
import time
from pathlib import Path
from unittest.mock import AsyncMock, patch

import httpx
import pytest

_PROJECT_ROOT = str(Path(__file__).resolve().parent.parent)
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

from tools.pipeline_tools import (
    FailedTask,
    PipelineAnalysisResult,
    PipelineTools,
    _build_auth_header,
    _is_test_step,
    _parse_build_identifier,
)
from utils.ado_token import _jwt_exp_seconds

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------


def _make_jwt(payload: dict) -> str:
    """Build a fake JWT with the given payload (no signature)."""
    header = base64.urlsafe_b64encode(b'{"alg":"none"}').rstrip(b"=").decode()
    body = base64.urlsafe_b64encode(json.dumps(payload).encode()).rstrip(b"=").decode()
    return f"{header}.{body}.sig"


FAKE_JWT = _make_jwt({"exp": int(time.time()) + 3600, "aud": "test"})


# ---------------------------------------------------------------------------
# _jwt_exp_seconds
# ---------------------------------------------------------------------------


class TestJwtExpSeconds:
    def test_valid_jwt(self) -> None:
        exp = int(time.time()) + 7200
        token = _make_jwt({"exp": exp})
        assert _jwt_exp_seconds(token) == exp

    def test_missing_exp(self) -> None:
        token = _make_jwt({"aud": "test"})
        assert _jwt_exp_seconds(token) is None

    def test_not_a_jwt(self) -> None:
        assert _jwt_exp_seconds("not-a-jwt") is None

    def test_malformed_base64(self) -> None:
        assert _jwt_exp_seconds("a.!!!.c") is None


# ---------------------------------------------------------------------------
# _parse_build_identifier
# ---------------------------------------------------------------------------


class TestParseBuildIdentifier:
    def test_bare_integer(self) -> None:
        build_id, proj = _parse_build_identifier("5094469")
        assert build_id == 5094469
        assert proj is None

    def test_ado_url(self) -> None:
        url = "https://dev.azure.com/azure-sdk/internal/_build/results?buildId=12345"
        build_id, proj = _parse_build_identifier(url)
        assert build_id == 12345
        assert proj == "internal"

    def test_public_url(self) -> None:
        url = "https://dev.azure.com/azure-sdk/public/_build/results?buildId=99"
        build_id, proj = _parse_build_identifier(url)
        assert build_id == 99
        assert proj == "public"

    def test_empty_raises(self) -> None:
        with pytest.raises(ValueError, match="Empty"):
            _parse_build_identifier("")

    def test_no_build_id_in_url_raises(self) -> None:
        with pytest.raises(ValueError, match="Could not extract buildId"):
            _parse_build_identifier("https://dev.azure.com/azure-sdk/internal/_build")

    def test_invalid_string_raises(self) -> None:
        with pytest.raises(ValueError, match="Invalid"):
            _parse_build_identifier("not-a-number-or-url")


# ---------------------------------------------------------------------------
# _build_auth_header
# ---------------------------------------------------------------------------


class TestBuildAuthHeader:
    def test_bearer_format(self) -> None:
        header = _build_auth_header("my-token")
        assert header == {"Authorization": "Bearer my-token"}


# ---------------------------------------------------------------------------
# _is_test_step
# ---------------------------------------------------------------------------


class TestIsTestStep:
    def test_test_in_name(self) -> None:
        assert _is_test_step("Run tests") is True

    def test_deploy_test_resources_excluded(self) -> None:
        assert _is_test_step("Deploy Test Resources") is False

    def test_none(self) -> None:
        assert _is_test_step(None) is False

    def test_no_test(self) -> None:
        assert _is_test_step("Build SDK") is False


# ---------------------------------------------------------------------------
# resolve_token
# ---------------------------------------------------------------------------


class TestResolveToken:
    @pytest.fixture(autouse=True)
    def _clear_cache(self) -> None:
        """Reset the module-level token cache between tests."""
        import utils.ado_token as mod

        mod._token_cache = None

    @pytest.mark.asyncio
    async def test_kv_token_used(self) -> None:
        with patch(
            "utils.ado_token.get_secret", new_callable=AsyncMock, return_value=FAKE_JWT
        ):
            from utils.ado_token import resolve_token

            token = await resolve_token()
            assert token == FAKE_JWT

    @pytest.mark.asyncio
    async def test_fallback_to_credential(self) -> None:
        mock_credential = AsyncMock()
        mock_credential.get_token.return_value = AsyncMock(token=FAKE_JWT)

        with (
            patch(
                "utils.ado_token.get_secret", new_callable=AsyncMock, return_value=None
            ),
            patch("utils.ado_token.get_credential", return_value=mock_credential),
        ):
            from utils.ado_token import resolve_token

            token = await resolve_token()
            assert token == FAKE_JWT
            mock_credential.get_token.assert_awaited_once()

    @pytest.mark.asyncio
    async def test_non_jwt_raises(self) -> None:
        with patch(
            "utils.ado_token.get_secret",
            new_callable=AsyncMock,
            return_value="not-a-jwt",
        ):
            from utils.ado_token import resolve_token

            with pytest.raises(RuntimeError, match="does not look like"):
                await resolve_token()

    @pytest.mark.asyncio
    async def test_cache_reuse(self) -> None:
        secret_mock = AsyncMock(return_value=FAKE_JWT)
        with patch("utils.ado_token.get_secret", secret_mock):
            from utils.ado_token import resolve_token

            t1 = await resolve_token()
            t2 = await resolve_token()
            assert t1 == t2
            # get_secret should only be called once due to caching.
            assert secret_mock.await_count == 1


# ---------------------------------------------------------------------------
# PipelineTools.azsdk_analyze_pipeline (integration-style with mocked HTTP)
# ---------------------------------------------------------------------------


def _mock_transport(responses: dict[str, httpx.Response]) -> httpx.MockTransport:
    """Build a MockTransport that routes by URL substring."""

    def handler(request: httpx.Request) -> httpx.Response:
        url = str(request.url)
        for pattern, resp in responses.items():
            if pattern in url:
                return resp
        return httpx.Response(404, json={"error": "not found"})

    return httpx.MockTransport(handler)


class TestAnalyzePipeline:
    @pytest.fixture(autouse=True)
    def _clear_token_cache(self) -> None:
        import utils.ado_token as mod

        mod._token_cache = None

    @pytest.mark.asyncio
    async def test_invalid_identifier(self) -> None:
        tools = PipelineTools()
        result = await tools.azsdk_analyze_pipeline(pipeline="", project="")
        assert result.success is False
        assert "Empty" in (result.error or "")

    @pytest.mark.asyncio
    async def test_token_failure(self) -> None:
        with patch(
            "tools.pipeline_tools.resolve_token",
            new_callable=AsyncMock,
            side_effect=RuntimeError("no token"),
        ):
            tools = PipelineTools()
            result = await tools.azsdk_analyze_pipeline(
                pipeline="123", project="public"
            )
            assert result.success is False
            assert "no token" in (result.error or "")

    @pytest.mark.asyncio
    async def test_successful_analysis(self) -> None:
        build_json = {
            "id": 100,
            "status": "completed",
            "result": "failed",
            "project": {"name": "public"},
        }
        timeline_json = {
            "records": [
                {
                    "type": "Task",
                    "result": "failed",
                    "name": "Build",
                    "log": {"id": 1},
                },
                {
                    "type": "Task",
                    "result": "failed",
                    "name": "Run tests",
                    "log": {"id": 2},
                },
            ]
        }
        log_text = "error CS1234: something went wrong"

        transport = _mock_transport(
            {
                "/builds/100?": httpx.Response(200, json=build_json),
                "/timeline": httpx.Response(200, json=timeline_json),
                "/logs/1": httpx.Response(200, text=log_text),
            }
        )

        with patch(
            "tools.pipeline_tools.resolve_token",
            new_callable=AsyncMock,
            return_value="fake",
        ):
            with patch(
                "httpx.AsyncClient", return_value=httpx.AsyncClient(transport=transport)
            ):
                tools = PipelineTools()
                result = await tools.azsdk_analyze_pipeline(
                    pipeline="100", project="public"
                )

        assert result.success is True
        assert result.build_id == 100
        assert result.status == "completed"
        assert result.result == "failed"
        # "Run tests" should be filtered out; only "Build" kept.
        assert len(result.failed_tasks) == 1
        assert result.failed_tasks[0].name == "Build"
        assert "CS1234" in result.failed_tasks[0].log_excerpt

    @pytest.mark.asyncio
    async def test_permission_error_on_resolve_project(self) -> None:
        transport = _mock_transport(
            {
                "/builds/999?": httpx.Response(203, text="<html>sign in</html>"),
            }
        )

        with patch(
            "tools.pipeline_tools.resolve_token",
            new_callable=AsyncMock,
            return_value="fake",
        ):
            with patch(
                "httpx.AsyncClient", return_value=httpx.AsyncClient(transport=transport)
            ):
                tools = PipelineTools()
                result = await tools.azsdk_analyze_pipeline(
                    pipeline="999", project="public"
                )

        assert result.success is False
        assert result.error is not None
