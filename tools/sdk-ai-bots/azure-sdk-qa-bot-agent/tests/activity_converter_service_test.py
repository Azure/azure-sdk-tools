"""Regression tests for Graph chatMessage to Bot Framework Activity conversion."""

from __future__ import annotations

import sys
from pathlib import Path

import pytest

_PROJECT_ROOT = str(Path(__file__).resolve().parent.parent)
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

from services.activity_converter_service import ActivityConverterService


def test_convert_to_activity_preserves_function_contract():
    chat_message = {
        "id": "message-id",
        "subject": "SDK question",
        "createdDateTime": "2026-09-23T12:34:56Z",
        "webUrl": "https://teams.microsoft.com/l/message/thread/123?tenantId=tenant-123&groupId=group-456",
        "locale": "en-US",
        "replyToId": "root-message-id",
        "body": {
            "contentType": "html",
            "content": "<p>Hello bot</p>",
        },
        "from": {
            "user": {
                "id": "user-id",
                "displayName": "Test User",
            }
        },
        "channelIdentity": {
            "teamId": "team-id",
            "channelId": "channel-id",
        },
        "mentions": [
            {
                "mentionText": "Azure SDK Q&A Bot",
                "mentioned": {
                    "application": {
                        "id": "bot-app-id",
                        "displayName": "Azure SDK Q&A Bot",
                    }
                },
            }
        ],
    }

    activity = ActivityConverterService().convert_to_activity(chat_message)

    assert activity["type"] == "message"
    assert activity["id"] == "message-id"
    assert activity["timestamp"] == "2026-09-23T12:34:56Z"
    assert activity["localTimestamp"] == "2026-09-23T12:34:56Z"
    assert activity["serviceUrl"] == "https://smba.trafficmanager.net/apac/tenant-123"
    assert activity["text"] == "<p>Hello bot</p>"
    assert activity["attachments"] == [{
        "contentType": "text/html",
        "content": "<p>Hello bot</p>",
    }]
    assert activity["from"] == {
        "id": "user-id",
        "name": "Test User",
        "aadObjectId": "user-id",
        "role": "user",
    }
    assert activity["conversation"] == {
        "name": "SDK question",
        "isGroup": True,
        "conversationType": "channel",
        "tenantId": "tenant-123",
        "id": "channel-id;messageid=root-message-id",
    }
    assert activity["recipient"] == {
        "id": "28:189f38c2-e2a7-433d-944e-b1166e5402c2",
        "name": "Azure SDK Q&A Bot",
    }
    assert activity["entities"] == [
        {
            "mentioned": {
                "id": "28:bot-app-id",
                "name": "Azure SDK Q&A Bot",
            },
            "text": "<at>Azure SDK Q&A Bot</at>",
            "type": "mention",
        },
        {
            "locale": "en-US",
            "country": "US",
            "platform": "Windows",
            "timezone": "Asia/Shanghai",
            "type": "clientInfo",
        },
    ]
    assert activity["channelData"] == {
        "teamsChannelId": "channel-id",
        "teamsTeamId": "team-id",
        "channel": {"id": "channel-id"},
        "tenant": {"id": "tenant-123"},
    }
    assert activity["locale"] == "en-US"
    assert activity["localTimezone"] == "Asia/Shanghai"
    assert activity["callerId"] == "urn:botframework:azure"


def test_convert_to_activity_uses_plain_text_without_html_attachment():
    activity = ActivityConverterService().convert_to_activity({
        "body": {
            "contentType": "text",
            "content": "Plain question",
        },
    })

    assert activity["text"] == "Plain question"
    assert activity["attachments"] == []
    assert activity["locale"] == "en-US"


@pytest.mark.parametrize("reply_to_id", [None, "root-message-id"])
@pytest.mark.parametrize("relative", [False, True])
def test_convert_to_activity_preserves_inline_images(reply_to_id, relative):
    base = (
        "https://graph.microsoft.com/v1.0/teams/team-id/channels/"
        "19%3Achannel%40thread.tacv2/messages/"
    )
    base += (
        "root-message-id/replies/message-id/"
        if reply_to_id else "message-id/"
    )
    image_url = base + "hostedContents/image-id/$value"
    source = "hostedContents/image-id/$value" if relative else image_url
    content = f'<p>Why did this fail?</p><img src="{source}" /><img src="{source}">'
    activity = ActivityConverterService().convert_to_activity({
        "id": "message-id",
        "replyToId": reply_to_id,
        "channelIdentity": {
            "teamId": "team-id",
            "channelId": "19:channel@thread.tacv2",
        },
        "body": {"contentType": "html", "content": content},
    })

    assert activity["text"] == content
    assert activity["attachments"] == [
        {"contentType": "text/html", "content": content},
        {"contentType": "image/*", "contentUrl": image_url},
    ]


def test_convert_to_activity_preserves_native_image_and_decodes_entities():
    source = "https://smba.trafficmanager.net/attachments/image?one=1&amp;two=2"
    activity = ActivityConverterService().convert_to_activity({
        "body": {"contentType": "html", "content": f'<IMG SRC="{source}">'},
    })
    assert activity["attachments"][1] == {
        "contentType": "image/*",
        "contentUrl": "https://smba.trafficmanager.net/attachments/image?one=1&two=2",
    }


@pytest.mark.parametrize("source", [
    "https://example.com/image.png",
    "http://smba.trafficmanager.net/image.png",
    "https://graph.microsoft.com/v1.0/me/photo/$value",
    "https://graph.microsoft.com:8443/v1.0/teams/t/channels/c/messages/m/hostedContents/i/$value",
    "data:image/png;base64,invalid",
    "hostedContents/image-id/$value",
    "https://[invalid",
])
def test_convert_to_activity_ignores_unsupported_image_sources(source, caplog):
    activity = ActivityConverterService().convert_to_activity({
        "body": {"contentType": "html", "content": f'<img src="{source}">'},
    })
    assert len(activity["attachments"]) == 1
    assert "Ignoring unsupported inline image" in caplog.text


@pytest.mark.parametrize("resource", [
    "teams/other-team/channels/channel/messages/message",
    "teams/team/channels/other-channel/messages/message",
    "teams/team/channels/channel/messages/other-message",
])
def test_convert_to_activity_does_not_fetch_images_from_another_message(resource, caplog):
    source = f"https://graph.microsoft.com/v1.0/{resource}/hostedContents/image/$value"
    activity = ActivityConverterService().convert_to_activity({
        "id": "message",
        "channelIdentity": {"teamId": "team", "channelId": "channel"},
        "body": {"contentType": "html", "content": f'<img src="{source}">'},
    })
    assert len(activity["attachments"]) == 1
    assert "Ignoring unsupported inline image" in caplog.text


def test_convert_to_activity_accepts_beta_image_url_for_source_message():
    source = (
        "https://graph.microsoft.com/beta/teams/team/channels/channel/messages/"
        "message/hostedContents/image/$value"
    )
    activity = ActivityConverterService().convert_to_activity({
        "id": "message",
        "channelIdentity": {"teamId": "team", "channelId": "channel"},
        "body": {"contentType": "html", "content": f'<img src="{source}">'},
    })
    assert activity["attachments"][1]["contentUrl"] == source


@pytest.mark.parametrize("body_type", ["html", "text"])
@pytest.mark.parametrize("name", ["image.png", "SCREENSHOT.JPG", "image.jpeg", "image.gif", "image.webp"])
def test_convert_to_activity_preserves_uploaded_image_reference(body_type, name):
    source = "https://contoso.sharepoint.com/teams/SDK/Shared Documents/image.png"
    content = '<p>Why did this fail?</p><attachment id="image-id"></attachment>'
    activity = ActivityConverterService().convert_to_activity({
        "id": "message",
        "body": {"contentType": body_type, "content": content},
        "attachments": [{
            "id": "image-id", "contentType": "reference", "contentUrl": source,
            "name": name, "content": None, "thumbnailUrl": None,
        }],
    })
    assert activity["text"] == content
    assert activity["attachments"][-1] == {"contentType": "image/*", "contentUrl": source}
    assert len(activity["attachments"]) == (2 if body_type == "html" else 1)


def test_convert_to_activity_deduplicates_inline_and_file_images():
    source = "https://contoso.sharepoint.com/teams/SDK/image.png"
    activity = ActivityConverterService().convert_to_activity({
        "body": {"contentType": "html", "content": f'<img src="{source}">'},
        "attachments": [
            {"contentType": "reference", "contentUrl": source, "name": "image.png"},
            {"contentType": "image/png", "contentUrl": source},
        ],
    })
    assert activity["attachments"] == [
        {"contentType": "text/html", "content": f'<img src="{source}">'},
        {"contentType": "image/*", "contentUrl": source},
    ]


@pytest.mark.parametrize("source", [
    "https://graph.microsoft.com/v1.0/drives/drive/items/image/content",
    "https://graph.microsoft.com/v1.0/shares/u!encoded/driveItem/content",
])
def test_convert_to_activity_preserves_graph_file_reference_without_message_metadata(source):
    activity = ActivityConverterService().convert_to_activity({
        "attachments": [{
            "contentType": "reference", "contentUrl": source, "name": "image.png",
        }],
    })
    assert activity["attachments"] == [{"contentType": "image/*", "contentUrl": source}]


@pytest.mark.parametrize("attachment", [
    {"contentType": "reference", "contentUrl": "https://contoso.sharepoint.com/file.pdf", "name": "file.pdf"},
    {"contentType": "reference", "contentUrl": "https://contoso.sharepoint.com/file.svg", "name": "file.svg"},
    {"contentType": "reference", "contentUrl": "https://example.com/image.png", "name": "image.png"},
    {"contentType": "reference", "contentUrl": "", "name": "image.png"},
])
def test_convert_to_activity_ignores_non_images_and_invalid_file_sources(attachment):
    activity = ActivityConverterService().convert_to_activity({"attachments": [attachment]})
    assert activity["attachments"] == []