"""Run the Teams channel backfill as a background job on the backend server.

Backfill is plain ETL: it pages the collection Logic App and writes what it
finds into ``conversation-messages``. That is the container this server already
fills from live Teams traffic, and no step of the import consults a model, so
the work belongs here rather than in the hosted collection agent.

A run walks entire channels and can take many minutes, far longer than a Teams
request may wait, so callers receive a job id immediately and poll for the
outcome. Only one run is accepted at a time because concurrent runs would page
the same channels into the same shared container for no gain.
"""

from __future__ import annotations

import asyncio
import json
import logging
from collections import OrderedDict
from datetime import datetime, timezone
from pathlib import Path
from uuid import uuid4

from config import app_config
from models.teams_backfill import (
    TeamsBackfillJob,
    TeamsBackfillRequest,
    TeamsBackfillStatus,
)
from services.teams_collection_service import (
    backfill_configured_channels,
    parse_timestamp,
    select_channels,
)
from utils.background_tasks import BackgroundTaskTracker

logger = logging.getLogger(__name__)

CONFIG_PATH = Path(__file__).resolve().parents[1] / "config" / "teams_collection_config.json"
MAX_TRACKED_JOBS = 50


class BackfillInProgressError(RuntimeError):
    """Raised when a backfill is requested while another one is still running."""


class TeamsBackfillService:
    """Own the single in-flight backfill run and the status callers poll for."""

    def __init__(self, config_path: Path = CONFIG_PATH, runner=backfill_configured_channels):
        self._config_path = config_path
        self._runner = runner
        self._jobs: OrderedDict[str, TeamsBackfillJob] = OrderedDict()
        self._running: str | None = None
        self._lock = asyncio.Lock()

    async def start(self, request: TeamsBackfillRequest) -> TeamsBackfillJob:
        """Accept or refuse a run before a single Teams page is fetched."""
        config = json.loads(self._config_path.read_text(encoding="utf-8"))
        select_channels(config["channels"], request.channel_id)
        if request.start_time is not None:
            parse_timestamp(request.start_time, "start_time")
        async with self._lock:
            if self._running is not None:
                raise BackfillInProgressError(
                    "A Teams backfill is already running; wait for it to finish."
                )
            job = TeamsBackfillJob(
                job_id=str(uuid4()),
                status=TeamsBackfillStatus.running,
                channel_id=request.channel_id,
                start_time=request.start_time,
                started_at=datetime.now(timezone.utc),
            )
            self._jobs[job.job_id] = job
            while len(self._jobs) > MAX_TRACKED_JOBS:
                self._jobs.popitem(last=False)
            self._running = job.job_id
        BackgroundTaskTracker.instance().track(
            asyncio.create_task(self._run(job, config, request))
        )
        return job

    def get(self, job_id: str) -> TeamsBackfillJob | None:
        """Return the live job state, or ``None`` once it has aged out."""
        return self._jobs.get(job_id)

    async def _run(
        self, job: TeamsBackfillJob, config: dict, request: TeamsBackfillRequest
    ) -> None:
        try:
            job.summary = await self._runner(
                config, app_config.get, request.channel_id, request.start_time
            )
            job.status = TeamsBackfillStatus.succeeded
        except asyncio.CancelledError:
            job.status = TeamsBackfillStatus.cancelled
            raise
        except Exception:
            logger.exception("Teams backfill job %s failed.", job.job_id)
            job.status = TeamsBackfillStatus.failed
            job.error = "Teams backfill failed; inspect the server logs."
        finally:
            job.completed_at = datetime.now(timezone.utc)
            self._running = None
