from __future__ import annotations

import asyncio
import json
import queue
import subprocess
import threading
from dataclasses import dataclass
from pathlib import Path
from typing import Any


class TypeSpecParseError(RuntimeError):
    pass


@dataclass(frozen=True, slots=True)
class TypeSpecDeclaration:
    start: int
    end: int
    start_line: int
    end_line: int
    name: str | None
    kind: str | None


class _Worker:
    def __init__(self, script: Path) -> None:
        self._process = subprocess.Popen(
            ["node", str(script)],
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            encoding="utf-8",
        )
        self._request_id = 0

    def parse(self, source: str) -> tuple[TypeSpecDeclaration, ...]:
        if self._process.stdin is None or self._process.stdout is None:
            raise TypeSpecParseError("TypeSpec parser worker streams are unavailable")
        self._request_id += 1
        self._process.stdin.write(
            json.dumps({"id": self._request_id, "source": source}) + "\n"
        )
        self._process.stdin.flush()
        response_line = self._process.stdout.readline()
        if not response_line:
            stderr = (
                self._process.stderr.read()
                if self._process.stderr is not None
                else ""
            )
            raise TypeSpecParseError(
                f"TypeSpec parser worker exited unexpectedly: {stderr.strip()}"
            )
        response = json.loads(response_line)
        if response.get("id") != self._request_id:
            raise TypeSpecParseError("TypeSpec parser worker returned an invalid response")
        if error := response.get("error"):
            raise TypeSpecParseError(str(error))
        return tuple(TypeSpecDeclaration(**item) for item in response["declarations"])

    def close(self) -> None:
        if self._process.poll() is not None:
            return
        if self._process.stdin is not None:
            self._process.stdin.close()
        try:
            self._process.wait(timeout=5)
        except subprocess.TimeoutExpired:
            self._process.terminate()
            self._process.wait(timeout=5)


class TypeSpecWorkerPool:
    def __init__(self, size: int = 2) -> None:
        script = Path(__file__).with_name("typespec_worker.mjs")
        self._workers = [_Worker(script) for _ in range(size)]
        self._available: queue.Queue[_Worker] = queue.Queue()
        self._closed = False
        self._lock = threading.Lock()
        for worker in self._workers:
            self._available.put(worker)

    async def parse(self, source: str) -> tuple[TypeSpecDeclaration, ...]:
        return await asyncio.to_thread(self._parse, source)

    def _parse(self, source: str) -> tuple[TypeSpecDeclaration, ...]:
        worker = self._available.get()
        try:
            return worker.parse(source)
        finally:
            self._available.put(worker)

    def close(self) -> None:
        with self._lock:
            if self._closed:
                return
            self._closed = True
        for worker in self._workers:
            worker.close()
