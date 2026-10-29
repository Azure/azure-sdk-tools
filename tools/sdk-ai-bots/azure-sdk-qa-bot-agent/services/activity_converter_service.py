"""Convert Microsoft Graph Teams messages into Bot Framework activities."""

from __future__ import annotations

from datetime import datetime, timezone
from html.parser import HTMLParser
import logging
from typing import Any
from urllib.parse import parse_qs, quote, unquote, urljoin, urlparse

from utils.teams_image import validate_teams_image_url

logger = logging.getLogger(__name__)


class _InlineImageParser(HTMLParser):
    def __init__(self) -> None:
        super().__init__(convert_charrefs=True)
        self.sources: list[str] = []

    def handle_starttag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        if tag == "img":
            source = dict(attrs).get("src")
            if source:
                self.sources.append(source)


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
            "attachments": self._convert_attachments(chat_message),
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

    def _convert_attachments(self, chat_message: dict[str, Any]) -> list[dict[str, Any]]:
        body = chat_message.get("body") or {}
        attachments: list[dict[str, Any]] = []
        parser = _InlineImageParser()
        if body.get("contentType") == "html":
            attachments.append({"contentType": "text/html", "content": body.get("content")})
            parser.feed(body.get("content") or "")
        sources = list(parser.sources)
        for attachment in chat_message.get("attachments") or []:
            content_type = attachment.get("contentType") or ""
            name = attachment.get("name") or ""
            if content_type.startswith("image/") or (
                content_type == "reference"
                and name.rsplit(".", 1)[-1].lower() in ("png", "jpg", "jpeg", "gif", "webp")
            ):
                if attachment.get("contentUrl"):
                    sources.append(attachment["contentUrl"])
                else:
                    logger.warning("Ignoring image attachment without a content URL")
        channel = chat_message.get("channelIdentity") or {}
        message_id = chat_message.get("id") or ""
        root_id = chat_message.get("replyToId") or message_id
        message_url = ""
        if channel.get("teamId") and channel.get("channelId") and message_id:
            message_url = (
                "https://graph.microsoft.com/v1.0/teams/"
                + quote(channel["teamId"], safe="")
                + "/channels/" + quote(channel["channelId"], safe="")
                + "/messages/" + quote(root_id, safe="")
            )
            if root_id != message_id:
                message_url += "/replies/" + quote(message_id, safe="")
            message_url += "/"
        seen: set[str] = set()
        for source in sources:
            try:
                url = urljoin(message_url, source)
                validate_teams_image_url(url)
                parsed = urlparse(url)
                if (
                    parsed.hostname == "graph.microsoft.com"
                    and parsed.path.split("/", 3)[2] == "teams"
                ):
                    if not message_url:
                        raise ValueError("Graph images require source message metadata")
                    image_message_path = parsed.path.split("/", 2)[2].rsplit(
                        "/hostedContents/", 1
                    )[0]
                    expected_path = urlparse(message_url).path.split("/", 2)[2].rstrip("/")
                    if unquote(image_message_path) != unquote(expected_path):
                        raise ValueError("Graph image must belong to the source message")
            except ValueError:
                logger.warning(
                    "Ignoring unsupported inline image or file attachment in message %s",
                    message_id,
                )
                continue
            if url not in seen:
                seen.add(url)
                # The actual MIME type is checked when the image is downloaded.
                attachments.append({"contentType": "image/*", "contentUrl": url})
        return attachments

    def _convert_mentions(self, mentions: list[dict[str, Any]]) -> list[dict[str, Any]]:
        converted_mentions: list[dict[str, Any]] = []
        for mention in mentions:
            application = ((mention.get("mentioned") or {}).get("application") or {})
            application_id = application.get("id")
            converted_mentions.append({
                "mentioned": {
                    "id": f"28:{application_id}" if application_id else "",
                    "name": application.get("displayName") or "",
                },
                "text": f"<at>{mention.get('mentionText')}</at>",
                "type": "mention",
            })
        return converted_mentions

    def _utc_now_iso(self) -> str:
        return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")