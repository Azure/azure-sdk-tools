"""Convert Microsoft Graph Teams messages into Bot Framework activities."""

from __future__ import annotations

from datetime import datetime, timezone
from typing import Any
from urllib.parse import parse_qs, urlparse


class ActivityConverterService:
    """Convert a Graph chatMessage payload to a Bot Framework Activity payload."""

    def convert_to_activity(self, chat_message: dict[str, Any]) -> dict[str, Any]:
        web_url = chat_message.get("webUrl") or ""
        tenant_id = self._extract_tenant_id(web_url)
        body = chat_message.get("body") or {}
        channel_identity = chat_message.get("channelIdentity") or {}
        from_user = ((chat_message.get("from") or {}).get("user") or {})
        created_at = chat_message.get("createdDateTime") or self._utc_now_iso()

        return {
            "label": chat_message.get("subject") or "",
            "valueType": "",
            "listenFor": [],
            "type": "message",
            "id": chat_message.get("id") or "",
            "timestamp": created_at,
            "localTimestamp": created_at,
            "channelId": "msteams",
            "serviceUrl": "https://smba.trafficmanager.net/apac/" + tenant_id,
            "text": body.get("content") or "",
            "textFormat": "plain",
            "attachments": self._convert_attachments(body),
            "from": {
                "id": from_user.get("id") or "",
                "name": from_user.get("displayName") or "",
                "aadObjectId": from_user.get("id") or "",
                "role": "user",
            },
            "conversation": {
                "name": chat_message.get("subject") or "",
                "isGroup": True,
                "conversationType": "channel",
                "tenantId": tenant_id,
                "id": (channel_identity.get("channelId") or "")
                + ";messageid="
                + (chat_message.get("replyToId") or ""),
            },
            "recipient": {
                "id": "28:189f38c2-e2a7-433d-944e-b1166e5402c2",
                "name": "Azure SDK Q&A Bot",
            },
            "entities": [
                *self._convert_mentions(chat_message.get("mentions") or []),
                {
                    "locale": chat_message.get("locale"),
                    "country": "US",
                    "platform": "Windows",
                    "timezone": "Asia/Shanghai",
                    "type": "clientInfo",
                },
            ],
            "channelData": {
                "teamsChannelId": channel_identity.get("channelId") or "",
                "teamsTeamId": channel_identity.get("teamId") or "",
                "channel": {
                    "id": channel_identity.get("channelId") or "",
                },
                "tenant": {
                    "id": tenant_id,
                },
            },
            "locale": chat_message.get("locale") or "en-US",
            "localTimezone": "Asia/Shanghai",
            "callerId": "urn:botframework:azure",
        }

    def _extract_tenant_id(self, web_url: str) -> str:
        query = parse_qs(urlparse(web_url).query)
        return query.get("tenantId", [""])[0]

    def _convert_attachments(self, body: dict[str, Any]) -> list[dict[str, Any]]:
        if body.get("contentType") != "html":
            return []
        return [{"contentType": "text/html", "content": body.get("content")}]

    def _convert_mentions(self, mentions: list[dict[str, Any]]) -> list[dict[str, Any]]:
        converted_mentions: list[dict[str, Any]] = []
        for mention in mentions:
            application = ((mention.get("mentioned") or {}).get("application") or {})
            converted_mentions.append({
                "mentioned": {
                    "id": "28:" + application.get("id") if application.get("id") else "",
                    "name": application.get("displayName") or "",
                },
                "text": f"<at>{mention.get('mentionText')}</at>",
                "type": "mention",
            })
        return converted_mentions

    def _utc_now_iso(self) -> str:
        return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")