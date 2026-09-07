"""Daily Chatbot Evolution Agent orchestration.

The pipeline ingests active Teams threads, invokes the Agent to evaluate every
eligible conversation, checks remediation issues, and invokes Agent validation
after an issue closes. Conversation completion and correctness are judged only
inside the Agent.

Usage::

    # Scan the last day (default) and drive the evolution loop
    python scripts/run_feedback_jobs.py

    # Scan a wider window
    python scripts/run_feedback_jobs.py --days 2
"""

from __future__ import annotations

import argparse
import asyncio
import logging
import os
import re
import sys
from datetime import datetime, timedelta, timezone
from pathlib import Path
from urllib.parse import quote

import httpx
import yaml
from dotenv import load_dotenv

_PROJECT_DIR = Path(__file__).resolve().parent.parent
load_dotenv(_PROJECT_DIR / ".env", override=False)

if str(_PROJECT_DIR) not in sys.path:
    sys.path.insert(0, str(_PROJECT_DIR))

import config.app_config as app_config
from models.feedback import (
    ChatbotEvolutionAgentMode,
    ChatbotEvolutionAgentOutcome,
)
from services.chatbot_evolution_agent_service import ChatbotEvolutionAgentService
from services.qa_record_service import QARecordService
from tools.github_mcp_tools import get_github_issue_state
from utils.azure_ai_foundry import close_clients as close_ai_clients
from utils.azure_cosmosdb import close_cosmos_client
from utils.azure_credential import close_credential
from utils.azure_storage import close_storage_client, download_blob

logger = logging.getLogger("run_feedback_jobs")

_TESTING_CHANNEL_PATTERN = re.compile(r"\btesting\b", re.IGNORECASE)
_TESTING_CHANNEL_NAMES = {
    "azure sdk qa bot - auto reply - test",
    "smoke-tests",
}
_DEFAULT_KNOWLEDGE_SYNC_PIPELINE = (
    "tools - sdk-ai-bots-knowledge-sync - provision-and-sync"
)


def _is_testing_channel(name: str) -> bool:
    normalized = name.strip().casefold()
    return (
        _TESTING_CHANNEL_PATTERN.search(normalized) is not None
        or normalized in _TESTING_CHANNEL_NAMES
    )


async def _load_excluded_channels() -> set[str]:
    """Return channel ids whose configured display name marks test traffic."""
    container = app_config.get("STORAGE_CONFIG_CONTAINER")
    blob = app_config.get("CHANNEL_CONFIG_BLOB")
    if not container or not blob:
        raise RuntimeError("Storage channel configuration is not configured")
    data = await download_blob(container, blob)
    if not data:
        raise RuntimeError("Storage channel configuration is empty")
    try:
        parsed = yaml.safe_load(data.decode("utf-8")) or {}
    except (UnicodeDecodeError, yaml.YAMLError) as exc:
        raise RuntimeError("Storage channel configuration is invalid") from exc
    excluded: set[str] = set()
    for entry in parsed.get("channels", []) or []:
        channel_id = entry.get("id")
        name = entry.get("name") or ""
        if channel_id and _is_testing_channel(name):
            excluded.add(channel_id)
    return excluded


def _parse_dt(value: str) -> datetime:
    dt = datetime.fromisoformat(value)
    if dt.tzinfo is None:
        dt = dt.replace(tzinfo=timezone.utc)
    return dt.astimezone(timezone.utc)


def _resolve_window(args: argparse.Namespace) -> tuple[datetime, datetime]:
    end = _parse_dt(args.end) if args.end else datetime.now(timezone.utc)
    start = _parse_dt(args.start) if args.start else end - timedelta(days=args.days)
    if start >= end:
        raise ValueError(f"start ({start}) must be before end ({end})")
    return start, end


async def _restore_candidate_knowledge(
    *,
    transport: httpx.AsyncBaseTransport | None = None,
) -> int:
    """Queue the sync-only candidate knowledge pipeline and await completion."""
    token = os.environ.get("SYSTEM_ACCESSTOKEN", "").strip()
    collection_uri = os.environ.get("ADO_COLLECTION_URI", "").strip().rstrip("/")
    project = os.environ.get("ADO_PROJECT", "").strip()
    source_branch = os.environ.get("ADO_SOURCE_BRANCH", "").strip() or "refs/heads/main"
    pipeline_name = os.environ.get(
        "KNOWLEDGE_SYNC_PIPELINE_NAME",
        _DEFAULT_KNOWLEDGE_SYNC_PIPELINE,
    ).strip()
    environment = os.environ.get("KNOWLEDGE_SYNC_ENVIRONMENT", "dev").strip()
    missing = [
        name
        for name, value in {
            "SYSTEM_ACCESSTOKEN": token,
            "ADO_COLLECTION_URI": collection_uri,
            "ADO_PROJECT": project,
            "KNOWLEDGE_SYNC_PIPELINE_NAME": pipeline_name,
            "KNOWLEDGE_SYNC_ENVIRONMENT": environment,
        }.items()
        if not value
    ]
    if missing:
        raise RuntimeError(
            "Cannot restore candidate knowledge; missing pipeline settings: "
            + ", ".join(missing)
        )

    api_root = f"{collection_uri}/{quote(project, safe='')}/_apis/build"
    auth = httpx.BasicAuth("", token)
    timeout_seconds = int(os.environ.get("KNOWLEDGE_SYNC_TIMEOUT_SECONDS", "7200"))
    deadline = asyncio.get_running_loop().time() + timeout_seconds

    async with httpx.AsyncClient(auth=auth, timeout=60, transport=transport) as client:
        definitions_response = await client.get(
            f"{api_root}/definitions",
            params={"name": pipeline_name, "api-version": "7.1"},
        )
        definitions_response.raise_for_status()
        definitions = [
            item
            for item in definitions_response.json().get("value", [])
            if item.get("name") == pipeline_name
        ]
        if len(definitions) != 1:
            raise RuntimeError(
                f"Expected one Azure DevOps pipeline named {pipeline_name!r}; "
                f"found {len(definitions)}"
            )

        queue_response = await client.post(
            f"{api_root}/builds",
            params={"api-version": "7.1"},
            json={
                "definition": {"id": definitions[0]["id"]},
                "sourceBranch": source_branch,
                "templateParameters": {
                    "environment": environment,
                    "provisionInfrastructure": "false",
                },
            },
        )
        queue_response.raise_for_status()
        build_id = int(queue_response.json()["id"])
        logger.info(
            "Queued candidate knowledge restore build %d using %s",
            build_id,
            pipeline_name,
        )

        while asyncio.get_running_loop().time() < deadline:
            status_response = await client.get(
                f"{api_root}/builds/{build_id}",
                params={"api-version": "7.1"},
            )
            status_response.raise_for_status()
            build = status_response.json()
            if build.get("status") == "completed":
                result = build.get("result")
                if result != "succeeded":
                    raise RuntimeError(
                        f"Candidate knowledge restore build {build_id} finished with {result}"
                    )
                logger.info("Candidate knowledge restore build %d succeeded", build_id)
                return build_id
            await asyncio.sleep(15)

    raise TimeoutError(
        f"Candidate knowledge restore build {build_id} did not finish within "
        f"{timeout_seconds} seconds"
    )


async def _validate_pending_records(
    qa_service: QARecordService,
    evolution_service: ChatbotEvolutionAgentService,
    excluded_channels: set[str],
    tenant_id: str | None,
    counts: dict[str, int],
) -> None:
    pending = await qa_service.list_pending_validation(tenant_id=tenant_id)
    for record in pending:
        if QARecordService.channel_key_of(record) in excluded_channels:
            counts["skipped"] += 1
            continue
        issue_url = record.feedback.issue_url if record.feedback else None
        if not issue_url:
            logger.error("Pending-validation record %s has no issue URL", record.id)
            counts["skipped"] += 1
            continue
        try:
            issue_state = await get_github_issue_state(issue_url)
        except Exception:
            logger.exception("Failed to read issue state for %s", record.id)
            counts["skipped"] += 1
            continue
        if issue_state != "closed":
            counts["waiting_validation"] += 1
            continue

        try:
            result = await evolution_service.run_job(
                record.id,
                record.tenant_id,
                mode=ChatbotEvolutionAgentMode.validation,
            )
        except Exception:
            logger.exception("Validation persistence failed for %s", record.id)
            counts["validation_failed"] += 1
            continue
        if result is None:
            counts["validation_failed"] += 1
        elif result.outcome == ChatbotEvolutionAgentOutcome.validation_passed:
            counts["validated"] += 1
        else:
            counts["validation_failed"] += 1


async def _run(args: argparse.Namespace) -> None:
    qa_service = QARecordService()
    evolution_service = ChatbotEvolutionAgentService()

    start, end = _resolve_window(args)
    logger.info(
        "Scanning conversations active in [%s, %s)",
        start.isoformat(),
        end.isoformat(),
    )
    excluded_channels = await _load_excluded_channels()

    # 1. Ingest threads active in the window into the durable QA status table.
    messages = await qa_service.get_messages_in_period(start, end)
    touched = await qa_service.upsert_threads_from_messages(
        messages,
        excluded_channels=excluded_channels,
    )
    logger.info("Upserted %d QA thread record(s)", len(touched))

    # Keep the complete Agent workflow behind one configuration gate so it can
    # be disabled without changing the scheduled pipeline.
    enabled = (
        app_config.get("CHATBOT_EVOLUTION_AGENT_ENABLED", "false").strip().lower()
        == "true"
    )
    if args.dry_run or not enabled:
        logger.info("Agent processing disabled; ingestion completed only")
        return

    counts = {
        "ongoing": 0,
        "finished": 0,
        "issues": 0,
        "waiting_validation": 0,
        "validated": 0,
        "validation_failed": 0,
        "evolution_failed": 0,
        "skipped": 0,
    }

    # 2. Restore candidate knowledge before analysis so a failed prior run
    # cannot leak temporary content into this run. Restore again afterward,
    # before any production validation, even when an analysis call fails.
    # each conversation. The pipeline does not make either decision itself.
    analyzable = await qa_service.list_analyzable(tenant_id=args.tenant)
    if args.limit is not None:
        analyzable = analyzable[: args.limit]
    if analyzable:
        await _restore_candidate_knowledge()
        try:
            for record in analyzable:
                if QARecordService.channel_key_of(record) in excluded_channels:
                    counts["skipped"] += 1
                    continue
                try:
                    result = await evolution_service.run_job(
                        record.id,
                        record.tenant_id,
                        mode=ChatbotEvolutionAgentMode.analysis,
                    )
                except Exception:
                    logger.exception("Analysis persistence failed for %s", record.id)
                    counts["skipped"] += 1
                    continue
                if result is None:
                    counts["skipped"] += 1
                elif result.outcome in (
                    ChatbotEvolutionAgentOutcome.processing_failed,
                    ChatbotEvolutionAgentOutcome.remediation_failed,
                ):
                    counts["evolution_failed"] += 1
                elif result.outcome == ChatbotEvolutionAgentOutcome.conversation_ongoing:
                    counts["ongoing"] += 1
                elif result.outcome == ChatbotEvolutionAgentOutcome.no_issue:
                    counts["finished"] += 1
                else:
                    counts["issues"] += 1
        finally:
            await _restore_candidate_knowledge()

    # 3. Validate closed issues only after candidate state is authoritative.
    # Issues created by this run remain open and are considered on a later run.
    await _validate_pending_records(
        qa_service,
        evolution_service,
        excluded_channels,
        args.tenant,
        counts,
    )

    logger.info(
        "Evolution scan complete: ongoing=%d finished=%d issues=%d "
        "waiting-validation=%d validated=%d validation-failed=%d "
        "evolution-failed=%d skipped=%d",
        counts["ongoing"],
        counts["finished"],
        counts["issues"],
        counts["waiting_validation"],
        counts["validated"],
        counts["validation_failed"],
        counts["evolution_failed"],
        counts["skipped"],
    )


async def main() -> None:
    parser = argparse.ArgumentParser(
        description="Ingest QA conversations and run the evolution loop.",
    )
    parser.add_argument(
        "--days",
        type=int,
        default=1,
        help="Look back this many days (default: 1).",
    )
    parser.add_argument("--start", type=str, default=None, help="Window start.")
    parser.add_argument("--end", type=str, default=None, help="Window end.")
    parser.add_argument("--tenant", type=str, default=None)
    parser.add_argument("--limit", type=int, default=None)
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Ingest records without invoking agents or checking issues.",
    )
    parser.add_argument("--verbose", "-v", action="store_true")
    args = parser.parse_args()

    logging.basicConfig(
        level=logging.DEBUG if args.verbose else logging.INFO,
        format="%(asctime)s %(levelname)s %(name)s: %(message)s",
        stream=sys.stdout,
    )
    logging.getLogger("azure.core").setLevel(logging.WARNING)
    logging.getLogger("azure.identity").setLevel(logging.WARNING)

    await app_config.init()
    try:
        await _run(args)
    finally:
        await _close_clients()


async def _close_clients() -> None:
    for closer in (
        close_ai_clients,
        close_cosmos_client,
        close_storage_client,
        close_credential,
    ):
        try:
            await closer()
        except Exception:
            logger.debug("Error closing client %s", closer.__name__, exc_info=True)


if __name__ == "__main__":
    asyncio.run(main())
