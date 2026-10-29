"""Hermetic coverage for Teams image authentication and bounded downloads."""

from __future__ import annotations

import base64
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
SHAREPOINT_IMAGE = (
    "https://contoso.sharepoint.com/teams/SDK/Shared Documents/Channel/image.png"
)
GRAPH_FILE = "https://graph.microsoft.com/v1.0/drives/drive/items/image/content"
DOWNLOAD_URL = "https://contoso.sharepoint.com/_layouts/15/download.aspx?token=secret"
PNG_BYTES = b"\x89PNG\r\n\x1a\n"
PNG_DATA_URI = "data:image/png;base64,iVBORw0KGgo="


@pytest.fixture
def credentials(monkeypatch):
    graph = MagicMock()
    graph.get_token = AsyncMock(return_value=SimpleNamespace(token="graph-test-token"))
    bot = MagicMock()
    bot.get_token = AsyncMock(return_value=SimpleNamespace(token="bot-test-token"))
    monkeypatch.setattr(teams_image, "get_credential", lambda: graph)
    monkeypatch.setattr(teams_image, "_get_bot_credential", lambda: bot)
    return graph, bot


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
    uri = f"data:{mime};base64,iVBORw0KGgo="
    assert await teams_image.get_image_data_uri(uri) == PNG_DATA_URI
    graph, bot = credentials
    graph.get_token.assert_not_awaited()
    bot.get_token.assert_not_awaited()


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
    graph, bot = credentials
    graph.get_token.assert_not_awaited()
    bot.get_token.assert_not_awaited()


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
    graph, bot = credentials
    graph.get_token.assert_not_awaited()
    bot.get_token.assert_not_awaited()


@pytest.mark.asyncio
@pytest.mark.parametrize("url", [
    GRAPH_IMAGE,
    GRAPH_FILE,
    "https://graph.microsoft.com/v1.0/shares/u!encoded/driveItem/content",
    GRAPH_IMAGE.replace("/v1.0/", "/beta/"),
    GRAPH_IMAGE.replace("/messages/message/", "/messages/root/replies/reply/"),
])
async def test_graph_image_uses_backend_graph_token(url, credentials, mock_download):
    requests = mock_download(
        lambda request: httpx.Response(
            200, headers={"content-type": "image/png; charset=binary"}, content=PNG_BYTES
        )
    )
    assert await teams_image.get_image_data_uri(url) == "data:image/png;base64,iVBORw0KGgo="
    graph, bot = credentials
    graph.get_token.assert_awaited_once_with("https://graph.microsoft.com/.default")
    bot.get_token.assert_not_awaited()
    assert requests[0].headers["authorization"] == "Bearer graph-test-token"


@pytest.mark.asyncio
async def test_native_image_keeps_bot_credentials(monkeypatch, credentials, mock_download):
    monkeypatch.setenv("BOT_TENANT_ID", "bot-tenant")
    requests = mock_download(
        lambda request: httpx.Response(
            200, headers={"content-type": "image/png"}, content=PNG_BYTES
        )
    )
    await teams_image.get_image_data_uri(BOT_IMAGE)
    graph, bot = credentials
    graph.get_token.assert_not_awaited()
    bot.get_token.assert_awaited_once_with(
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
    "https://graph.microsoft.com/v1.0/me/photo/$value",
    "https://example.com/image.png",
    "https://contoso.sharepoint.com.example.com/image.png",
    "https://sharepoint.com/image.png",
    "https://contoso.sharepoint.com/",
    "https://contoso.sharepoint.com:8443/image.png",
    "https://user@contoso.sharepoint.com/image.png",
    "https://contoso.sharepoint.com/image.png#fragment",
    "https://contoso.sharepoint.com\\@example.com/image.png",
    "https://contoso.sharepoint.com/\nimage.png",
    GRAPH_FILE.replace("/items/image/", "/items/%2e%2e/"),
    GRAPH_FILE.replace("/items/image/", "/items/a%2Fb/"),
    GRAPH_FILE.replace("/items/image/", "/items/a%5Cb/"),
    GRAPH_FILE + "?query=value",
    GRAPH_FILE.replace("/content", ""),
])
async def test_invalid_urls_are_rejected_before_credentials(url, credentials):
    with pytest.raises(ValueError):
        await teams_image.get_image_data_uri(url)
    graph, bot = credentials
    graph.get_token.assert_not_awaited()
    bot.get_token.assert_not_awaited()


@pytest.mark.asyncio
async def test_image_redirect_is_not_followed(credentials, mock_download):
    requests = mock_download(
        lambda request: httpx.Response(
            302, headers={"location": "https://example.com/image.png"}
        )
    )
    with pytest.raises(httpx.HTTPStatusError):
        await teams_image.get_image_data_uri(GRAPH_IMAGE)
    assert len(requests) == 1


@pytest.mark.asyncio
@pytest.mark.parametrize("status", [403, 404, 429, 500])
async def test_download_errors_are_not_hidden(status, credentials, mock_download):
    mock_download(lambda request: httpx.Response(status))
    with pytest.raises(httpx.HTTPStatusError):
        await teams_image.get_image_data_uri(GRAPH_IMAGE)


@pytest.mark.asyncio
@pytest.mark.parametrize(("content_type", "content"), [
    ("image/jpeg", b"\xff\xd8\xfftest"),
    ("image/gif", b"GIF89atest"),
    ("image/webp", b"RIFF\x00\x00\x00\x00WEBP"),
])
async def test_supported_image_formats(content_type, content, credentials, mock_download):
    mock_download(
        lambda request: httpx.Response(
            200, headers={"content-type": content_type}, content=content
        )
    )
    assert (await teams_image.get_image_data_uri(GRAPH_IMAGE)).startswith(
        f"data:{content_type};base64,"
    )


@pytest.mark.asyncio
@pytest.mark.parametrize(("headers", "content"), [
    ({"content-type": "text/html"}, b"<html>not an image</html>"),
    ({"content-type": "image/png"}, b"not a PNG"),
    ({"content-type": "image/png"}, b""),
    ({}, PNG_BYTES),
    ({"content-type": "image/png", "content-length": "99999999"}, PNG_BYTES),
])
async def test_invalid_image_response_is_rejected(headers, content, credentials, mock_download):
    mock_download(lambda request: httpx.Response(200, headers=headers, content=content))
    with pytest.raises(ValueError):
        await teams_image.get_image_data_uri(GRAPH_IMAGE)


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
        await teams_image.get_image_data_uri(GRAPH_IMAGE)


@pytest.mark.asyncio
@pytest.mark.parametrize("source", [
    SHAREPOINT_IMAGE,
    SHAREPOINT_IMAGE.replace("contoso.", "contoso-my."),
    SHAREPOINT_IMAGE.replace("Channel", "Channel – SDK") + "?web=1",
])
async def test_sharepoint_file_is_resolved_through_graph(source, credentials, mock_download):
    requests = mock_download(
        lambda request: httpx.Response(
            302, headers={"location": DOWNLOAD_URL}
        ) if request.url.host == "graph.microsoft.com" else httpx.Response(
            200, headers={"content-type": "application/octet-stream"}, content=PNG_BYTES
        )
    )
    assert await teams_image.get_image_data_uri(source) == "data:image/png;base64,iVBORw0KGgo="
    token = "u!" + base64.urlsafe_b64encode(
        str(httpx.URL(source)).encode("utf-8")
    ).decode("ascii").rstrip("=")
    assert str(requests[0].url) == (
        f"https://graph.microsoft.com/v1.0/shares/{token}/driveItem/content"
    )
    assert requests[0].headers["authorization"] == "Bearer graph-test-token"
    assert str(requests[1].url) == DOWNLOAD_URL
    assert "authorization" not in requests[1].headers
    graph, bot = credentials
    graph.get_token.assert_awaited_once_with("https://graph.microsoft.com/.default")
    bot.get_token.assert_not_awaited()


@pytest.mark.asyncio
@pytest.mark.parametrize("destination", [
    DOWNLOAD_URL,
    "https://b0mpua-by3301.files.1drv.com/image?token=secret",
])
async def test_graph_file_redirect_drops_graph_credentials(destination, credentials, mock_download):
    def handle(request):
        if request.url.host == "graph.microsoft.com":
            return httpx.Response(302, headers={"location": destination})
        return httpx.Response(200, headers={"content-type": "image/png"}, content=PNG_BYTES)

    requests = mock_download(handle)
    assert await teams_image.get_image_data_uri(GRAPH_FILE) == "data:image/png;base64,iVBORw0KGgo="
    assert len(requests) == 2
    assert "authorization" not in requests[1].headers


@pytest.mark.asyncio
@pytest.mark.parametrize("destination", [
    "https://example.com/image.png",
    "http://contoso.sharepoint.com/image.png",
    "https://contoso.sharepoint.com.example.com/image.png",
    "https://127.0.0.1/image.png",
    "https://169.254.169.254/image.png",
    "https://user:password@contoso.sharepoint.com/image.png",
    "https://contoso.sharepoint.com:8443/image.png",
    "/relative/download",
    "",
])
async def test_graph_file_rejects_unsafe_redirects(destination, credentials, mock_download):
    requests = mock_download(
        lambda request: httpx.Response(302, headers={"location": destination})
    )
    with pytest.raises(ValueError):
        await teams_image.get_image_data_uri(GRAPH_FILE)
    assert len(requests) == 1


@pytest.mark.asyncio
async def test_graph_file_does_not_follow_redirect_chains(credentials, mock_download):
    requests = mock_download(
        lambda request: httpx.Response(302, headers={"location": DOWNLOAD_URL})
    )
    with pytest.raises(httpx.HTTPStatusError):
        await teams_image.get_image_data_uri(GRAPH_FILE)
    assert len(requests) == 2
    assert "authorization" not in requests[1].headers


@pytest.mark.asyncio
@pytest.mark.parametrize("status", [403, 404])
async def test_sharepoint_resolution_errors_are_not_hidden(status, credentials, mock_download):
    requests = mock_download(lambda request: httpx.Response(status))
    with pytest.raises(httpx.HTTPStatusError):
        await teams_image.get_image_data_uri(SHAREPOINT_IMAGE)
    assert len(requests) == 1


@pytest.mark.asyncio
@pytest.mark.parametrize("content", [b"<html>Sign in</html>", b"not an image"])
async def test_binary_non_images_are_rejected(content, credentials, mock_download):
    mock_download(lambda request: httpx.Response(
        200, headers={"content-type": "application/octet-stream"}, content=content
    ))
    with pytest.raises(ValueError, match="do not match"):
        await teams_image.get_image_data_uri(GRAPH_FILE)


@pytest.mark.asyncio
async def test_sharepoint_download_limit_is_enforced(monkeypatch, credentials, mock_download):
    monkeypatch.setattr(teams_image, "MAX_IMAGE_BYTES", len(PNG_BYTES) - 1)
    mock_download(
        lambda request: httpx.Response(
            302, headers={"location": DOWNLOAD_URL}
        ) if request.url.host == "graph.microsoft.com" else httpx.Response(
            200, headers={"content-type": "image/png"}, content=PNG_BYTES
        )
    )
    with pytest.raises(ValueError, match="5 MiB limit"):
        await teams_image.get_image_data_uri(SHAREPOINT_IMAGE)


@pytest.mark.asyncio
async def test_converted_inline_image_reaches_model_as_pixels(credentials, mock_download):
    from models.chat import AdditionalInfo
    from services.activity_converter_service import ActivityConverterService
    from services.chat_service import ChatService

    requests = mock_download(
        lambda request: httpx.Response(
            200, headers={"content-type": "image/png"}, content=PNG_BYTES
        )
    )
    activity = ActivityConverterService().convert_to_activity({
        "id": "message",
        "channelIdentity": {"teamId": "team", "channelId": "channel"},
        "body": {
            "contentType": "html",
            "content": '<p>What error is visible?</p><img src="hostedContents/image/$value">',
        },
    })
    # Match the existing TypeScript bot's image attachment forwarding contract.
    infos = [
        AdditionalInfo(type="image", content="", link=attachment["contentUrl"])
        for attachment in activity["attachments"]
        if attachment["contentType"].startswith("image/")
    ]
    items = await ChatService()._build_additional_info_items(infos)

    assert str(requests[0].url) == GRAPH_IMAGE
    assert items[0]["content"][1]["image_url"] == "data:image/png;base64,iVBORw0KGgo="


@pytest.mark.asyncio
async def test_uploaded_image_reaches_model_as_pixels(credentials, mock_download):
    from models.chat import AdditionalInfo
    from services.activity_converter_service import ActivityConverterService
    from services.chat_service import ChatService

    mock_download(
        lambda request: httpx.Response(
            302, headers={"location": DOWNLOAD_URL}
        ) if request.url.host == "graph.microsoft.com" else httpx.Response(
            200, headers={"content-type": "image/png"}, content=PNG_BYTES
        )
    )
    activity = ActivityConverterService().convert_to_activity({
        "id": "message",
        "body": {
            "contentType": "html",
            "content": '<p>Why did CI fail?</p><attachment id="image-id"></attachment>',
        },
        "attachments": [{
            "id": "image-id", "contentType": "reference",
            "contentUrl": SHAREPOINT_IMAGE, "name": "image.png", "content": None,
        }],
    })
    infos = [
        AdditionalInfo(type="image", content="", link=attachment["contentUrl"])
        for attachment in activity["attachments"]
        if attachment["contentType"].startswith("image/")
    ]
    items = await ChatService()._build_additional_info_items(infos)
    assert len(items) == 1
    assert items[0]["content"][1]["image_url"] == "data:image/png;base64,iVBORw0KGgo="


@pytest.mark.asyncio
async def test_download_failure_notice_does_not_log_signed_url(credentials, mock_download, caplog):
    from models.chat import AdditionalInfo
    from services.chat_service import ChatService

    mock_download(
        lambda request: httpx.Response(
            302, headers={"location": DOWNLOAD_URL}
        ) if request.url.host == "graph.microsoft.com" else httpx.Response(403)
    )
    items = await ChatService()._build_additional_info_items([
        AdditionalInfo(type="image", content="", link=SHAREPOINT_IMAGE)
    ])
    assert "could not be downloaded" in items[0]["content"]
    assert "Failed to fetch Teams image" in caplog.text
    assert "HTTPStatusError" in caplog.text
    assert DOWNLOAD_URL not in caplog.text
    assert "secret" not in caplog.text
