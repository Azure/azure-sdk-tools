"""Unified background-task tracker for fire-and-forget asyncio tasks.

Provides a process-wide singleton via ``BackgroundTaskTracker.instance()``
so any module can track tasks without plumbing an instance through
constructors.
"""

from __future__ import annotations

import asyncio
import logging

logger = logging.getLogger(__name__)


class BackgroundTaskTracker:
    """Track fire-and-forget ``asyncio.Task`` objects and drain them on shutdown."""

    _instance: BackgroundTaskTracker | None = None

    def __init__(self) -> None:
        self._tasks: set[asyncio.Task] = set()

    @classmethod
    def instance(cls) -> BackgroundTaskTracker:
        """Return the process-wide singleton, creating it on first call."""
        if cls._instance is None:
            cls._instance = cls()
        return cls._instance

    def track(self, task: asyncio.Task) -> None:
        """Keep a strong reference to *task* until it finishes."""
        self._tasks.add(task)
        task.add_done_callback(self._tasks.discard)

    async def shutdown(self) -> None:
        """Cancel and await all in-flight tasks."""
        pending = [t for t in self._tasks if not t.done()]
        if not pending:
            return
        logger.info("Cancelling %d background task(s)", len(pending))
        for t in pending:
            t.cancel()
        await asyncio.gather(*pending, return_exceptions=True)
