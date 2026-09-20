"""LLM-backed, versioned processing for complete Teams threads."""

from __future__ import annotations

import json
from datetime import datetime, timezone

from models.teams_processing import ThreadProcessingDecision


class TeamsThreadProcessor:
    NAME = "teams-channel-qa-summary"

    def __init__(self, agent, version: str):
        if not isinstance(version, str) or not version.strip():
            raise ValueError("Teams processing requires a nonempty version.")
        self._agent = agent
        self.version = version.strip()

    @staticmethod
    def _processing_scope(channel: dict) -> dict[str, str]:
        scope = channel.get("processingScope")
        if not isinstance(scope, dict):
            raise ValueError("Each channel requires a processingScope name and description.")
        name = scope.get("name")
        description = scope.get("description")
        if (not isinstance(name, str) or not name.strip()
                or not isinstance(description, str) or not description.strip()):
            raise ValueError("Each channel requires a processingScope name and description.")
        return {"name": name, "description": description}

    @classmethod
    def validate_channel(cls, channel: dict) -> None:
        cls._processing_scope(channel)

    def is_current(self, processing: dict | None, source_content_hash: str) -> bool:
        return bool(
            isinstance(processing, dict)
            and processing.get("processor") == self.NAME
            and processing.get("processor_version") == self.version
            and processing.get("source_content_hash") == source_content_hash
        )

    async def process(self, channel: dict, post: dict, replies: list[dict],
                      source_content_hash: str) -> dict:
        scope = self._processing_scope(channel)
        payload = {
            "channel": {
                "name": scope["name"],
                "scope": scope["description"],
            },
            "post": post,
            "replies": replies,
        }
        response = await self._agent.run(json.dumps(
            payload, ensure_ascii=False, separators=(",", ":")
        ))
        value = getattr(response, "value", None)
        if isinstance(value, ThreadProcessingDecision):
            decision = value
        elif value is not None:
            decision = ThreadProcessingDecision.model_validate(value)
        else:
            decision = ThreadProcessingDecision.model_validate_json(response.text)
        subject = post.get("subject")
        if (decision.qa is not None and isinstance(subject, str) and subject.strip()
                and decision.qa.title != subject):
            raise ValueError("Processed Q&A title must preserve the original post subject.")
        return {
            "schema_version": 1,
            "processor": self.NAME,
            "processor_version": self.version,
            "source_content_hash": source_content_hash,
            "processed_at": datetime.now(timezone.utc).isoformat(),
            **decision.model_dump(mode="json"),
        }
