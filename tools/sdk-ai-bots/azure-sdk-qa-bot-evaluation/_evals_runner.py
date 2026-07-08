"""Foundry evaluation runner (completion mode).

We call the bot ``/completion`` HTTP endpoint **concurrently** (bounded by
``max_concurrency``), collect each answer + ``full_context`` + references, then grade
them as **inline** eval data where the builtin LLM evaluators read
``{{item.response}}`` / ``{{item.context}}``. Driving the slow answer-generation step
ourselves is the main lever for evaluation speed.

Flow: concurrent ``/completion`` collection -> ``evals.create`` (inline item source)
-> ``evals.runs.create`` -> poll -> ``output_items.list`` -> ``output_items_to_rows``
-> the existing ``EvalsResult`` gate/baseline logic.

"""

from __future__ import annotations

import asyncio
import json
import logging
import os
import subprocess
import time
from pathlib import Path
from typing import Any, Optional

from _evals_result import EvalsResult
from eval.criteria import build_testing_criteria

logger = logging.getLogger(__name__)

# Channel/tenant text mirrors the bot's _build_tenant_system_message so /completion
# routes the same way it does in production.
SCENARIO_TO_CHANNEL: dict[str, str] = {
    "typespec": "TypeSpec Discussion",
    "python": "Language - Python",
    "advocacy": "Advocacy",
    "ai": "AI Discussion",
    "apispec": "API Spec Review",
    "apiview": "APIView",
    "onboarding": "Azure SDK Onboarding",
    "go": "Language - Go",
    "java": "Language - Java",
    "net": "Language - DotNet",
    "javascript": "Language - JavaScript",
    "general": "General",
    "releasesupport": "SDK release support",
}

# Composite weights
BOT_EVALS_WEIGHTS = {"similarity": 0.6, "response_completeness": 0.4}

# Item schema for the inline eval data: the answer/context are collected from
# /completion and carried in the item (read via {{item.response}} / {{item.context}}).
COMPLETION_ITEM_SCHEMA: dict[str, Any] = {
    "type": "object",
    "properties": {
        "testcase": {"type": "string"},
        "query": {"type": "string"},
        "ground_truth": {"type": "string"},
        "response": {"type": "string"},
        "context": {"type": "string"},
        "expected_references": {"type": "array", "items": {"type": "object"}},
        "expected_knowledges": {"type": "array", "items": {"type": "object"}},
        "references": {"type": "array", "items": {"type": "object"}},
        "knowledges": {"type": "array", "items": {"type": "object"}},
    },
    "required": ["query", "response"],
}


def _completion_item(it: dict[str, Any]) -> dict[str, Any]:
    """Project a collected record into the eval ``item`` payload (JSON-safe)."""
    return {
        "testcase": it.get("testcase", "unknown"),
        "query": it.get("query", ""),
        "ground_truth": it.get("ground_truth", ""),
        "response": it.get("response", ""),
        "context": it.get("context", "") or "",
        "expected_references": it.get("expected_references", []),
        "expected_knowledges": it.get("expected_knowledges", []),
        "references": it.get("references", []),
        "knowledges": it.get("knowledges", []),
    }


def extract_title_and_link_from_references(references: list[dict[str, Any]]) -> list[dict[str, Any]]:
    """Map ``/completion`` reference objects to ``[{title, link}]`` (case-tolerant)."""
    if not references:
        return []
    refs: list[dict[str, Any]] = []
    for ref in references:
        title = ""
        link = ""
        if isinstance(ref, dict):
            title = ref.get("title") or ref.get("Title") or ""
            link = ref.get("link") or ref.get("Link") or ""
        refs.append({"title": title, "link": link})
    return refs


def extract_title_and_link_from_context(context: str) -> list[dict[str, Any]]:
    """Parse the ``full_context`` JSON string into ``[{title, link}]`` knowledge hits."""
    if not context:
        return []
    docs: list[dict[str, Any]] = []
    try:
        docs_obj = json.loads(context)
        for doc in docs_obj:
            title = ""
            link = ""
            if isinstance(doc, dict):
                title = doc.get("document_title") or doc.get("title") or ""
                link = doc.get("document_link") or doc.get("link") or ""
            docs.append({"title": title, "link": link})
    except (json.JSONDecodeError, TypeError) as exc:
        logger.warning("Failed to parse full_context JSON: %s", exc)
    return docs


class CompletionCollector:
    """Concurrently collect bot answers from the ``/completion`` HTTP endpoint.

    The slow part of evaluation is the bot generating an answer (full RAG pipeline);
    driving ``/completion`` ourselves with bounded concurrency parallelizes that step.
    """

    def __init__(
        self,
        *,
        api_url: str,
        access_token: str | None,
        max_concurrency: int = 8,
        timeout_seconds: int = 600,
        max_retries: int = 4,
    ) -> None:
        self._api_url = api_url
        self._access_token = access_token
        self._max_concurrency = max(1, max_concurrency)
        self._timeout_seconds = timeout_seconds
        self._max_retries = max_retries

    def collect(self, records: list[dict[str, Any]], tenant_id: str | None) -> list[dict[str, Any]]:
        """Return enriched items (one per input record) in original order.

        Records whose bot call fails are dropped (logged).
        """
        return asyncio.run(self._collect_async(records, tenant_id))

    async def _collect_async(self, records: list[dict[str, Any]], tenant_id: str | None) -> list[dict[str, Any]]:
        import aiohttp

        semaphore = asyncio.Semaphore(self._max_concurrency)
        results: list[dict[str, Any] | None] = [None] * len(records)
        logger.info(
            "🚀 Collecting %d bot responses with concurrency=%d via %s",
            len(records),
            self._max_concurrency,
            self._api_url,
        )

        async def _run(session: "aiohttp.ClientSession", index: int, record: dict[str, Any]) -> None:
            async with semaphore:
                start = time.time()
                try:
                    api_response = await self._call_bot_api(record["query"], tenant_id, session)
                except Exception as exc:  # noqa: BLE001
                    logger.error("❌ collect failed for %s: %s", record.get("testcase", "?"), exc)
                    return
                answer = api_response.get("answer", "")
                full_context = api_response.get("full_context", "") or ""
                references = api_response.get("references", []) or []
                results[index] = {
                    "testcase": record.get("testcase", "unknown"),
                    "query": record["query"],
                    "ground_truth": record.get("ground_truth", ""),
                    "response": answer,
                    "response_id": api_response.get("id", ""),
                    "context": full_context,
                    "latency": time.time() - start,
                    "response_length": len(answer),
                    "expected_references": record.get("expected_references", []),
                    "references": extract_title_and_link_from_references(references),
                    "expected_knowledges": record.get("expected_knowledges", []),
                    "knowledges": extract_title_and_link_from_context(full_context),
                }

        timeout = aiohttp.ClientTimeout(total=self._timeout_seconds)
        async with aiohttp.ClientSession(timeout=timeout) as session:
            await asyncio.gather(*(_run(session, i, r) for i, r in enumerate(records)))

        collected = [r for r in results if r is not None]
        logger.info("Collected %d/%d bot responses.", len(collected), len(records))
        return collected

    async def _call_bot_api(self, question: str, tenant_id: str | None, session: Any) -> Any:
        import aiohttp

        headers = {"Content-Type": "application/json; charset=utf8"}
        if self._access_token:
            headers["Authorization"] = f"Bearer {self._access_token}"
        payload = {
            "tenant_id": tenant_id if tenant_id is not None else "general_qa_bot",
            "message": {"role": "user", "content": question},
            "with_full_context": True,
        }
        last_status: int | None = None
        for attempt in range(self._max_retries):
            try:
                async with session.post(self._api_url, json=payload, headers=headers) as resp:
                    if resp.status == 200:
                        return await resp.json()
                    last_status = resp.status
                    if resp.status in (400, 422):
                        raise Exception(f"API request failed with status {resp.status} (non-retryable)")
            except aiohttp.ClientError as exc:
                last_status = -1
                logger.warning("⚠️  transport error attempt %d/%d: %s", attempt + 1, self._max_retries, exc)
            if attempt < self._max_retries - 1:
                await asyncio.sleep(2 ** attempt * 5)
        raise Exception(f"API request failed with status {last_status}")


def resolve_completion_url() -> str:
    """Resolve the bot ``/completion`` URL from env (default local server)."""
    endpoint = os.environ.get("BOT_SERVICE_ENDPOINT")
    if endpoint:
        return f"{endpoint.rstrip('/')}/completion"
    return "http://localhost:8089/completion"


def resolve_bot_token() -> str | None:
    """Resolve a bearer token for the bot service.

    Uses ``BOT_AGENT_TOKEN_RESOURCE`` via ``az account get-access-token`` when set,
    else falls back to ``BOT_AGENT_ACCESS_TOKEN``. Local server needs no token.
    """
    resource = os.environ.get("BOT_AGENT_TOKEN_RESOURCE")
    if resource:
        try:
            out = subprocess.run(
                ["az", "account", "get-access-token", "--resource", resource,
                 "--query", "accessToken", "-o", "tsv"],
                capture_output=True, text=True, check=True, shell=True,
            )
            return out.stdout.strip()
        except Exception as exc:  # noqa: BLE001
            logger.warning("⚠️  failed to get bot token via az cli: %s", exc)
    return os.environ.get("BOT_AGENT_ACCESS_TOKEN")


def retrieve_channel_tenant_map(credential: Any) -> dict[str, str]:
    """Load the channel->tenant map from the bot config blob (for /completion routing)."""
    from azure.storage.blob import BlobServiceClient

    account = os.environ["STORAGE_BLOB_ACCOUNT"]
    container = os.environ["BOT_CONFIG_CONTAINER"]
    blob_name = os.environ["BOT_CONFIG_CHANNEL_BLOB"]
    service = BlobServiceClient(account_url=f"https://{account}.blob.core.windows.net", credential=credential)
    blob = service.get_container_client(container).get_blob_client(blob_name)
    import yaml

    data = yaml.safe_load(blob.download_blob().readall())
    mapping: dict[str, str] = {"default": data["default"]["tenant"]}
    for item in data.get("channels") or []:
        mapping[item["name"]] = item["tenant"]
    return mapping


def resolve_tenant_for_scenario(scenario: str, channel_tenant_map: dict[str, str] | None) -> str | None:
    """Resolve the bot tenant_id for a scenario from the channel->tenant map."""
    if not channel_tenant_map:
        return None
    channel = SCENARIO_TO_CHANNEL.get(scenario, scenario)
    return channel_tenant_map.get(channel) or channel_tenant_map.get("default")


def _coerce_score(value: Any) -> float:
    try:
        f = float(value)
        return f if f == f else 0.0  # NaN guard
    except (TypeError, ValueError):
        return 0.0


def _get(obj: Any, attr: str, default: Any) -> Any:
    """Attribute or dict access against SDK models or plain dicts."""
    if isinstance(obj, dict):
        return obj.get(attr, default)
    return getattr(obj, attr, default)


def output_items_to_rows(
    output_items: list[Any],
    evaluators: list[str],
    *,
    threshold: float = 3.0,
) -> dict[str, Any]:
    """Adapt OpenAI-evals ``output_items`` into the legacy ``{"rows": [...]}`` shape.

    Each output item exposes ``datasource_item`` (the inline input row incl. the
    collected ``response``/``references``) and ``results`` (one grader result per
    criterion with ``name``/``score``/``passed``). We emit ``inputs.*`` and
    ``outputs.<metric>.<metric>`` / ``outputs.<metric>.<metric>_result`` keys
    consumed by ``EvalsResult``.
    """
    rows: list[dict[str, Any]] = []
    want_bot_evals = "bot_evals" in evaluators

    for oi in output_items:
        item = _get(oi, "datasource_item", {}) or {}
        results = _get(oi, "results", []) or []

        row: dict[str, Any] = {
            "inputs.testcase": item.get("testcase", item.get("query", "unknown")),
            "inputs.ground_truth": item.get("ground_truth", ""),
            "inputs.expected_references": item.get("expected_references", []),
            "inputs.expected_knowledges": item.get("expected_knowledges", []),
            "inputs.response": item.get("response", ""),
            "inputs.references": item.get("references", []) or [],
            "inputs.knowledges": item.get("knowledges", []) or [],
        }

        per_metric: dict[str, float] = {}
        for r in results:
            name = _get(r, "name", None)
            if not name:
                continue
            score = _coerce_score(_get(r, "score", 0.0))
            passed = bool(_get(r, "passed", False))
            per_metric[name] = score
            row[f"outputs.{name}.{name}"] = score
            row[f"outputs.{name}.{name}_result"] = "pass" if passed else "fail"

        if want_bot_evals and ("similarity" in per_metric or "response_completeness" in per_metric):
            composite = (
                per_metric.get("similarity", 0.0) * BOT_EVALS_WEIGHTS["similarity"]
                + per_metric.get("response_completeness", 0.0) * BOT_EVALS_WEIGHTS["response_completeness"]
            )
            row["outputs.bot_evals.bot_evals"] = composite
            row["outputs.bot_evals.bot_evals_similarity"] = per_metric.get("similarity", 0.0)
            row["outputs.bot_evals.bot_evals_response_completeness"] = per_metric.get(
                "response_completeness", 0.0
            )
            row["outputs.bot_evals.bot_evals_result"] = "pass" if composite >= threshold else "fail"

        rows.append(row)

    return {"rows": rows}


class FoundryEvalsRunner:
    """Collect bot answers concurrently and grade them inline on the Foundry project."""

    def __init__(
        self,
        evaluators: list[str],
        evals_result: EvalsResult,
        *,
        model: str,
        threshold: int = 3,
        poll_interval: float = 5.0,
        poll_timeout: float = 1800.0,
        max_concurrency: int = 8,
        completion_url: str | None = None,
        access_token: str | None = None,
    ) -> None:
        self._evaluators = evaluators
        self._evals_result = evals_result
        self._model = model
        self._threshold = threshold
        self._poll_interval = poll_interval
        self._poll_timeout = poll_timeout
        self._max_concurrency = max_concurrency
        self._completion_url = completion_url
        self._access_token = access_token

    @property
    def evals_result(self) -> EvalsResult:
        return self._evals_result

    def _poll_and_adapt(
        self,
        openai_client: Any,
        eval_id: str,
        run_id: str,
        scenario: str,
        extra_failed_rows: list[dict[str, Any]] | None = None,
    ) -> dict[str, Any]:
        """Shared poll-to-terminal + output_items -> rows -> record step.

        ``extra_failed_rows`` are appended before recording so that cases which never
        reached the grader (e.g. dropped /completion collection failures) still count
        as failures in the gate rather than vanishing from the denominator.
        """
        deadline = time.time() + self._poll_timeout
        run = openai_client.evals.runs.retrieve(run_id=run_id, eval_id=eval_id)
        while run.status not in ("completed", "failed"):
            if time.time() > deadline:
                raise TimeoutError(f"Eval run {run_id} did not finish within {self._poll_timeout}s")
            time.sleep(self._poll_interval)
            run = openai_client.evals.runs.retrieve(run_id=run_id, eval_id=eval_id)
            logger.info("  run status: %s", run.status)

        report_url = getattr(run, "report_url", None)
        if report_url:
            logger.info("Report URL: %s", report_url)
        if run.status == "failed":
            raise RuntimeError(f"Eval run {run_id} failed")

        output_items = list(openai_client.evals.runs.output_items.list(run_id=run_id, eval_id=eval_id))
        raw = output_items_to_rows(output_items, self._evaluators, threshold=float(self._threshold))
        if extra_failed_rows:
            raw["rows"].extend(extra_failed_rows)
        return {f"{scenario}_{run_id}": self._evals_result.record_run_result(raw)}

    def _failed_row(self, record: dict[str, Any]) -> dict[str, Any]:
        """A synthetic result row that fails every requested metric (uncollected case)."""
        row: dict[str, Any] = {
            "inputs.testcase": record.get("testcase", record.get("query", "unknown")),
            "inputs.ground_truth": record.get("ground_truth", ""),
            "inputs.expected_references": record.get("expected_references", []),
            "inputs.expected_knowledges": record.get("expected_knowledges", []),
            "inputs.response": "",
            "inputs.references": [],
            "inputs.knowledges": [],
        }
        for name in self._evaluators:
            row[f"outputs.{name}.{name}"] = 0.0
            row[f"outputs.{name}.{name}_result"] = "fail"
        return row

    def evaluate_run_completion(
        self,
        openai_client: Any,
        records: list[dict[str, Any]],
        scenario: str,
        *,
        tenant_id: str | None = None,
        evaluation_name: Optional[str] = None,
    ) -> dict[str, Any]:
        """Completion mode: concurrently collect bot answers, then grade them inline."""
        from openai.types.eval_create_params import DataSourceConfigCustom
        from openai.types.evals.create_eval_jsonl_run_data_source_param import (
            CreateEvalJSONLRunDataSourceParam,
            SourceFileContent,
            SourceFileContentContent,
        )

        # 1) Collect bot answers concurrently (the slow, now-parallelized step).
        collector = CompletionCollector(
            api_url=self._completion_url or resolve_completion_url(),
            access_token=self._access_token,
            max_concurrency=self._max_concurrency,
        )
        items = collector.collect(records, tenant_id)

        # Cases whose /completion call failed must still count as failures so the
        # gate is not blind to collection errors (agent mode grades every row).
        collected_keys = [it.get("testcase") for it in items]
        seen: dict[Any, int] = {}
        for key in collected_keys:
            seen[key] = seen.get(key, 0) + 1
        dropped: list[dict[str, Any]] = []
        for record in records:
            rkey = record.get("testcase", record.get("query", "unknown"))
            if seen.get(rkey, 0) > 0:
                seen[rkey] -= 1
            else:
                dropped.append(record)
        if dropped:
            logger.error(
                "❌ %d/%d cases had no bot response (counted as failures): %s",
                len(dropped),
                len(records),
                ", ".join(str(r.get("testcase", "?")) for r in dropped),
            )
        failed_rows = [self._failed_row(r) for r in dropped]

        # 2) Build criteria that read the answer/context from the inline item.
        testing_criteria = build_testing_criteria(
            self._evaluators, model=self._model, threshold=self._threshold
        )
        if not testing_criteria:
            logger.warning("No testing criteria built for evaluators=%s", self._evaluators)
            return {}

        # If nothing was collected, skip the Foundry run and report all-failed rows.
        if not items:
            logger.error("No bot responses collected for scenario=%s; all cases fail.", scenario)
            if not failed_rows:
                return {}
            raw = {"rows": failed_rows}
            return {f"{scenario}_no-responses": self._evals_result.record_run_result(raw)}

        data_source_config = DataSourceConfigCustom(
            type="custom", item_schema=COMPLETION_ITEM_SCHEMA, include_sample_schema=False
        )
        name = evaluation_name or f"qa-bot-{scenario}"
        eval_object = openai_client.evals.create(
            name=name,
            data_source_config=data_source_config,
            testing_criteria=testing_criteria,  # type: ignore[arg-type]
        )
        logger.info("Evaluation created (id=%s, name=%s)", eval_object.id, name)

        # 3) Grade the pre-collected answers as inline data (no agent target).
        content = [SourceFileContentContent(item=_completion_item(it)) for it in items]
        data_source = CreateEvalJSONLRunDataSourceParam(
            type="jsonl", source=SourceFileContent(type="file_content", content=content)
        )
        run = openai_client.evals.runs.create(
            eval_id=eval_object.id, name=f"{name}-run", data_source=data_source  # type: ignore[arg-type]
        )
        logger.info("Evaluation run created (id=%s)", run.id)
        return self._poll_and_adapt(openai_client, eval_object.id, run.id, scenario, failed_rows)


def resolve_records(dataset_spec: str, *, script_dir: Path) -> tuple[list[dict[str, Any]], str]:
    """Resolve ``--dataset`` to ``(records, scenario)`` for completion mode (local read).

    Accepts:
      * ``<path>.jsonl``              -> read that file; scenario = file stem.
      * ``qa-bot-<target>-<scenario>[:version]`` -> read ``evaluation_datasets/<target>/<scenario>.jsonl``.
    """
    candidate = Path(dataset_spec)
    if dataset_spec.endswith(".jsonl") or candidate.exists():
        path = candidate if candidate.is_absolute() else (script_dir / candidate)
        scenario = path.stem
    else:
        name = dataset_spec.split(":", 1)[0]
        # name form: qa-bot-<target>-<scenario>
        parts = name.split("-")
        if len(parts) < 4 or parts[0] != "qa" or parts[1] != "bot":
            raise ValueError(
                f"--dataset {dataset_spec!r} not a local path and not a qa-bot-<target>-<scenario> name"
            )
        target = parts[2]
        scenario = "-".join(parts[3:])
        path = script_dir / "evaluation_datasets" / target / f"{scenario}.jsonl"

    if not path.exists():
        raise FileNotFoundError(f"Dataset file not found for completion mode: {path}")

    records: list[dict[str, Any]] = []
    with path.open("r", encoding="utf-8") as fh:
        for line in fh:
            line = line.strip()
            if line:
                records.append(json.loads(line))
    return records, scenario


__all__ = [
    "FoundryEvalsRunner",
    "CompletionCollector",
    "output_items_to_rows",
    "resolve_records",
    "resolve_completion_url",
    "resolve_bot_token",
    "retrieve_channel_tenant_map",
    "resolve_tenant_for_scenario",
    "extract_title_and_link_from_references",
    "extract_title_and_link_from_context",
    "COMPLETION_ITEM_SCHEMA",
]
