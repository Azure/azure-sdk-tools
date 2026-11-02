"""Hermetic coverage for connector bytes and bounded Bot Framework downloads."""

from __future__ import annotations

from types import SimpleNamespace
from unittest.mock import AsyncMock, MagicMock

import httpx
import pytest

from utils import teams_image

GRAPH_IMAGE = (
    "https://graph.microsoft.com/v1.0/teams/team/channels/channel/"
    "messages/message/hostedContents/image/$value"
)
BOT_IMAGE = "https://smba.trafficmanager.net/attachments/image"
PNG_BYTES = b"\x89PNG\r\n\x1a\n"
PNG_DATA_URI = "data:image/png;base64,iVBORw0KGgo="


@pytest.fixture
def credentials(monkeypatch):
    bot = MagicMock()
    bot.get_token = AsyncMock(return_value=SimpleNamespace(token="bot-test-token"))
    monkeypatch.setattr(teams_image, "_get_bot_credential", lambda: bot)
    return bot


@pytest.fixture
def mock_download(monkeypatch):
    client_type = httpx.AsyncClient

    def install(handler):
        requests = []

        def capture(request):
            requests.append(request)
            return handler(request)

        transport = httpx.MockTransport(capture)
        monkeypatch.setattr(
            teams_image.httpx,
            "AsyncClient",
            lambda **kwargs: client_type(transport=transport, **kwargs),
        )
        return requests

    return install


@pytest.mark.asyncio
@pytest.mark.parametrize("mime", ["image/png", "application/octet-stream"])
async def test_connector_image_bytes_require_no_credentials_or_network(mime, credentials, monkeypatch):
    def unexpected_client(**kwargs):
        pytest.fail("Connector-supplied image bytes must not be downloaded again")

    monkeypatch.setattr(teams_image.httpx, "AsyncClient", unexpected_client)
    assert await teams_image.get_image_data_uri(f"data:{mime};base64,iVBORw0KGgo=") == PNG_DATA_URI
    credentials.get_token.assert_not_awaited()


@pytest.mark.asyncio
@pytest.mark.parametrize("uri", [
    "data:image/png,not-base64",
    "data:image/png;base64",
    "data:image/png;base64,",
    "data:image/png;base64,%%%invalid",
    "data:image/png;base64,iVBORw0KGgo",
    "data:image/png;base64,iVBORw0K\nGgo=",
    "data:text/html;base64,iVBORw0KGgo=",
    "data:image/svg+xml;base64,iVBORw0KGgo=",
    "data:image/jpeg;base64,iVBORw0KGgo=",
    "data:image/png;base64,bm90IGFuIGltYWdl",
    "data:application/octet-stream;base64,bm90IGFuIGltYWdl",
])
async def test_invalid_connector_image_data_is_rejected(uri, credentials):
    with pytest.raises(ValueError):
        await teams_image.get_image_data_uri(uri)
    credentials.get_token.assert_not_awaited()


@pytest.mark.asyncio
@pytest.mark.parametrize("limit", [4, len(PNG_BYTES) - 1])
async def test_connector_image_enforces_decoded_and_encoded_limits(limit, credentials, monkeypatch):
    monkeypatch.setattr(teams_image, "MAX_IMAGE_BYTES", limit)
    with pytest.raises(ValueError, match="5 MiB limit"):
        await teams_image.get_image_data_uri(PNG_DATA_URI)


@pytest.mark.asyncio
async def test_connector_image_at_exact_limit_is_accepted(credentials, monkeypatch):
    monkeypatch.setattr(teams_image, "MAX_IMAGE_BYTES", len(PNG_BYTES))
    assert await teams_image.get_image_data_uri(PNG_DATA_URI) == PNG_DATA_URI


@pytest.mark.asyncio
async def test_connector_image_reaches_model_without_graph_access(credentials):
    from models.chat import AdditionalInfo
    from services.chat_service import ChatService

    items = await ChatService()._build_additional_info_items([
        AdditionalInfo(type="image", content="", link=PNG_DATA_URI)
    ])
    assert items[0]["content"][1]["image_url"] == PNG_DATA_URI
    credentials.get_token.assert_not_awaited()


@pytest.mark.parametrize("url", [
    GRAPH_IMAGE,
    GRAPH_IMAGE.replace("/v1.0/", "/beta/"),
    GRAPH_IMAGE.replace("/messages/message/", "/messages/root/replies/reply/"),
])
def test_graph_hosted_content_is_still_valid_for_activity_conversion(url):
    teams_image.validate_teams_image_url(url)


@pytest.mark.asyncio
@pytest.mark.parametrize("url", [
    GRAPH_IMAGE,
    "https://graph.microsoft.com/v1.0/drives/drive/items/image/content",
    "https://graph.microsoft.com/v1.0/shares/u!encoded/driveItem/content",
    "https://contoso.sharepoint.com/teams/SDK/image.png",
])
async def test_backend_rejects_graph_and_sharepoint_without_credentials_or_network(
    url, credentials, monkeypatch,
):
    def unexpected_client(**kwargs):
        pytest.fail("Backend must not download Graph or SharePoint images")

    monkeypatch.setattr(teams_image.httpx, "AsyncClient", unexpected_client)
    with pytest.raises(ValueError):
        await teams_image.get_image_data_uri(url)
    credentials.get_token.assert_not_awaited()


@pytest.mark.asyncio
async def test_native_image_keeps_bot_credentials(monkeypatch, credentials, mock_download):
    monkeypatch.setenv("BOT_TENANT_ID", "bot-tenant")
    requests = mock_download(
        lambda request: httpx.Response(
            200, headers={"content-type": "image/png; charset=binary"}, content=PNG_BYTES
        )
    )
    assert await teams_image.get_image_data_uri(BOT_IMAGE) == PNG_DATA_URI
    credentials.get_token.assert_awaited_once_with(
        "https://api.botframework.com/.default", tenant_id="bot-tenant"
    )
    assert requests[0].headers["authorization"] == "Bearer bot-test-token"


@pytest.mark.asyncio
@pytest.mark.parametrize("url", [
    GRAPH_IMAGE.replace("https:", "http:"),
    GRAPH_IMAGE.replace("graph.microsoft.com", "graph.microsoft.com.example.com"),
    GRAPH_IMAGE.replace("graph.microsoft.com", "user:password@graph.microsoft.com"),
    GRAPH_IMAGE.replace("graph.microsoft.com", "@graph.microsoft.com"),
    GRAPH_IMAGE.replace("graph.microsoft.com", "graph.microsoft.com:8443"),
    GRAPH_IMAGE + "#fragment",
    GRAPH_IMAGE + "?$expand=anything",
    GRAPH_IMAGE.replace("/hostedContents/image/$value", ""),
    GRAPH_IMAGE.replace("/messages/message/", "/messages/%2e%2e/"),
    GRAPH_IMAGE.replace("/messages/message/", "/messages/a%2Fb/"),
    GRAPH_IMAGE.replace("/messages/message/", "/messages/a%5Cb/"),
    "https://graph.microsoft.com/v1.0/me/photo/$value",
    "https://example.com/image.png",
    "https://contoso.sharepoint.com/image.png",
    BOT_IMAGE.replace("https:", "http:"),
    BOT_IMAGE.replace("smba.trafficmanager.net", "user@smba.trafficmanager.net"),
    BOT_IMAGE.replace("smba.trafficmanager.net", "smba.trafficmanager.net:8443"),
    BOT_IMAGE + "#fragment",
    BOT_IMAGE + "\\image",
    BOT_IMAGE + "\nimage",
])
async def test_invalid_urls_are_rejected_before_credentials(url, credentials):
    with pytest.raises(ValueError):
        await teams_image.get_image_data_uri(url)
    credentials.get_token.assert_not_awaited()


@pytest.mark.asyncio
async def test_image_redirect_is_not_followed(credentials, mock_download):
    requests = mock_download(
        lambda request: httpx.Response(
            302, headers={"location": "https://example.com/image.png"}
        )
    )
    with pytest.raises(httpx.HTTPStatusError):
        await teams_image.get_image_data_uri(BOT_IMAGE)
    assert len(requests) == 1


@pytest.mark.asyncio
@pytest.mark.parametrize("status", [403, 404, 429, 500])
async def test_download_errors_are_not_hidden(status, credentials, mock_download):
    mock_download(lambda request: httpx.Response(status))
    with pytest.raises(httpx.HTTPStatusError):
        await teams_image.get_image_data_uri(BOT_IMAGE)


@pytest.mark.asyncio
@pytest.mark.parametrize(("content_type", "content"), [
    ("image/jpeg", b"\xff\xd8\xfftest"),
    ("image/gif", b"GIF89atest"),
    ("image/webp", b"RIFF\x00\x00\x00\x00WEBP"),
    ("application/octet-stream", PNG_BYTES),
])
async def test_supported_image_formats(content_type, content, credentials, mock_download):
    mock_download(
        lambda request: httpx.Response(
            200, headers={"content-type": content_type}, content=content
        )
    )
    expected_type = "image/png" if content_type == "application/octet-stream" else content_type
    assert (await teams_image.get_image_data_uri(BOT_IMAGE)).startswith(
        f"data:{expected_type};base64,"
    )


@pytest.mark.asyncio
@pytest.mark.parametrize(("headers", "content"), [
    ({"content-type": "text/html"}, b"<html>not an image</html>"),
    ({"content-type": "image/png"}, b"not a PNG"),
    ({"content-type": "image/png"}, b""),
    ({}, PNG_BYTES),
    ({"content-type": "image/png", "content-length": "99999999"}, PNG_BYTES),
    ({"content-type": "application/octet-stream"}, b"not an image"),
])
async def test_invalid_image_response_is_rejected(headers, content, credentials, mock_download):
    mock_download(lambda request: httpx.Response(200, headers=headers, content=content))
    with pytest.raises(ValueError):
        await teams_image.get_image_data_uri(BOT_IMAGE)


@pytest.mark.asyncio
async def test_chunked_download_enforces_byte_limit(monkeypatch, credentials, mock_download):
    class ImageStream(httpx.AsyncByteStream):
        async def __aiter__(self):
            yield PNG_BYTES
            yield b"too large"

    monkeypatch.setattr(teams_image, "MAX_IMAGE_BYTES", len(PNG_BYTES))
    mock_download(
        lambda request: httpx.Response(
            200, headers={"content-type": "image/png"}, stream=ImageStream()
        )
    )
    with pytest.raises(ValueError, match="5 MiB limit"):
        await teams_image.get_image_data_uri(BOT_IMAGE)


@pytest.mark.asyncio
async def test_unresolved_graph_image_produces_notice_without_logging_url(credentials, caplog):
    from models.chat import AdditionalInfo
    from services.chat_service import ChatService

    items = await ChatService()._build_additional_info_items([
        AdditionalInfo(type="image", content="", link=GRAPH_IMAGE)
    ])
    assert "could not be downloaded" in items[0]["content"]
    assert "Failed to fetch Teams image" in caplog.text
    assert "ValueError" in caplog.text
    assert GRAPH_IMAGE not in caplog.text
    credentials.get_token.assert_not_awaited()
