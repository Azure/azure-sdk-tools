"""Regression tests for Graph chatMessage to Bot Framework Activity conversion."""

from __future__ import annotations

import sys
from pathlib import Path

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