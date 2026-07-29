"""Entry point: run QA bot evaluation on the Foundry OpenAI-evals surface.

We call the bot ``/completion`` endpoint **concurrently** (bounded by
``--max_concurrency``), collect each answer + retrieved context + references, then
grade them as inline eval data where the builtin LLM evaluators read
``{{item.response}}`` / ``{{item.context}}``. Parallelizing the slow
answer-generation step is the main lever for evaluation speed.

Examples:
    # Run all configured evaluators against the typespec basic dataset:
    python evals_run.py --dataset qa-bot-basic-typespec:latest \
        --evaluators bot_evals,groundedness --max_concurrency 8

    # Or point at a local curated file directly:
    python evals_run.py --dataset evaluation_datasets/basic/typespec.jsonl
"""

from __future__ import annotations

import argparse
import json
import logging
import os
import sys
from datetime import datetime
from pathlib import Path
from typing import Any

from dotenv import load_dotenv

from _evals_result import EvalsResult, VerificationResult
from _evals_runner import (
    FoundryEvalsRunner,
    resolve_bot_token,
    resolve_completion_url,
    resolve_records,
    resolve_tenant_for_scenario,
    retrieve_channel_tenant_map,
)
from dataset._storage import credential_for

ALL_EVALUATORS = [
    "similarity",
    "response_completeness",
    "groundedness",
    "relevance",
    "coherence",
    "fluency",
    "bot_evals",
]

# Output fields surfaced per evaluator in the result table.
OUTPUT_FIELDS: dict[str, list[str]] = {
    "similarity": ["similarity"],
    "response_completeness": ["response_completeness"],
    "groundedness": ["groundedness"],
    "relevance": ["relevance"],
    "coherence": ["coherence"],
    "fluency": ["fluency"],
    "bot_evals": ["bot_evals", "bot_evals_similarity", "bot_evals_response_completeness", "bot_evals_result"],
}


def _load_suppression(script_dir: Path) -> dict[str, list[str]]:
    suppression: dict[str, list[str]] = {"evaluators": [], "testcases": []}
    f = script_dir / "suppression.json"
    if f.exists():
        try:
            loaded = json.loads(f.read_text(encoding="utf-8"))
            for key in ("evaluators", "testcases"):
                val = loaded.get(key, [])
                if isinstance(val, list):
                    suppression[key] = [str(x) for x in val]
        except (json.JSONDecodeError, TypeError) as exc:
            logging.warning("Failed to parse suppression.json: %s", exc)
    return suppression


def _dataset_label(dataset_spec: str, scenario: str) -> str:
    """Stable dataset identity for the evaluation name.

    ``qa-bot-<target>-<scenario>[:version]`` -> ``qa-bot-<target>-<scenario>``;
    a ``.../<target>/<scenario>.jsonl`` path -> ``qa-bot-<target>-<scenario>``;
    anything else falls back to ``qa-bot-<scenario>``.
    """
    base = dataset_spec.split(":", 1)[0]
    if base.endswith(".jsonl"):
        p = Path(base)
        target = p.parent.name
        return f"qa-bot-{target}-{p.stem}" if target else f"qa-bot-{p.stem}"
    if base.startswith("qa-bot-"):
        return base
    return f"qa-bot-{scenario}"


def main(argv: list[str] | None = None) -> int:
    # Ensure emoji in log/table output don't crash a non-UTF-8 console (e.g. Windows cp1252).
    for stream in (sys.stdout, sys.stderr):
        try:
            stream.reconfigure(encoding="utf-8")  # type: ignore[attr-defined]
        except (AttributeError, ValueError):
            pass
    logging.basicConfig(level=logging.INFO, stream=sys.stdout, format="%(asctime)s - %(levelname)s - %(message)s")
    logging.info("🚀 Starting evaluation ...")

    parser = argparse.ArgumentParser(description="Run QA bot evaluation on the Foundry evals surface.")
    parser.add_argument("--dataset", required=True, help="<path.jsonl> | qa-bot-<target>-<scenario>[:version]")
    parser.add_argument("--max_concurrency", type=int, default=8, help="parallel /completion calls")
    parser.add_argument("--evaluators", type=str, default=None, help="comma-separated subset; default all")
    parser.add_argument(
        "--run_context",
        type=str,
        default="local",
        help="context tag appended to the dataset name (e.g. 'local' or the pipeline name)",
    )
    parser.add_argument("--baseline_check", type=str, default="True")
    parser.add_argument("--is_ci", type=str, default="True")
    parser.add_argument("--cache_result", type=str, default="none", help="none | score | full")
    args = parser.parse_args(argv)

    is_ci = args.is_ci.lower() in ("true", "1", "yes", "on")
    baseline_check = args.baseline_check.lower() in ("true", "1", "yes")
    evaluators = args.evaluators.split(",") if args.evaluators else list(ALL_EVALUATORS)

    load_dotenv()
    script_dir = Path(__file__).resolve().parent

    try:
        endpoint = os.environ["AZURE_AI_PROJECT_ENDPOINT"]
        model = os.environ["AZURE_EVALUATION_MODEL_NAME"]
        threshold = int(os.environ.get("EVALUATE_THRESHOLD", "3"))
    except KeyError as exc:
        logging.error("Missing required environment variable: %s", exc)
        return 1

    metrics = {e: OUTPUT_FIELDS.get(e) for e in evaluators}
    suppression = _load_suppression(script_dir)
    evals_result = EvalsResult(metrics=metrics, suppressions=suppression)

    runner = FoundryEvalsRunner(
        evaluators=evaluators,
        evals_result=evals_result,
        model=model,
        threshold=threshold,
        max_concurrency=args.max_concurrency,
        completion_url=resolve_completion_url(),
        access_token=resolve_bot_token(),
    )

    from azure.ai.projects import AIProjectClient

    credential = credential_for(is_ci)
    all_results: dict[str, Any] = {}

    try:
        with AIProjectClient(endpoint=endpoint, credential=credential) as project_client:
            openai_client = project_client.get_openai_client()

            records, scenario = resolve_records(args.dataset, script_dir=script_dir)
            logging.info("Resolved %d records for scenario=%s", len(records), scenario)
            # Stable evaluation name = dataset identity + run context (local / pipeline),
            # so the Foundry list is not flooded with date/build/random-suffixed entries.
            dataset_label = _dataset_label(args.dataset, scenario)
            name = f"{dataset_label}-{args.run_context}"
            tenant_map = None
            try:
                tenant_map = retrieve_channel_tenant_map(credential)
            except Exception as exc:  # noqa: BLE001
                logging.warning("Could not load channel->tenant map (using default routing): %s", exc)
            tenant_id = resolve_tenant_for_scenario(scenario, tenant_map)
            all_results = runner.evaluate_run_completion(
                openai_client, records, scenario, tenant_id=tenant_id, evaluation_name=name
            )

        _cache_results(args.cache_result, script_dir, all_results, metrics, suppression, evals_result)

        evals_result.show_results(all_results, baseline_check)
        if baseline_check:
            evals_result.establish_baseline(all_results, is_ci)
        verdict = evals_result.verify_results(all_results, baseline_check)
        if verdict == VerificationResult.PASS_WITH_WARNING:
            print("##vso[task.logissue type=warning]Evaluation succeeded with warning. Some tests failed but suppressed.")
        elif verdict == VerificationResult.FAIL:
            logging.error("Evaluation failed; see the published failed-cases artifact for details.")
            return 1
    except Exception as exc:  # noqa: BLE001
        logging.exception("❌ Error occurred: %s", exc)
        return 1
    return 0


def _cache_results(
    mode: str,
    script_dir: Path,
    all_results: dict[str, Any],
    metrics: dict[str, Any],
    suppression: dict[str, list[str]],
    evals_result: EvalsResult,
) -> None:
    mode = mode.lower()
    if mode == "score":
        now = datetime.now()
        out = script_dir / f"evaluate-result-{now.strftime('%Y-%m-%d-%H-%S')}"
        with out.open("a", encoding="utf-8") as fh:
            for name, test_results in all_results.items():
                fh.write(f"\n-----------{name}----------------------\n")
                fh.write(evals_result.build_output_table(test_results))
        return
    if mode != "full":
        return

    now = datetime.now()
    cache_dir = script_dir / "cache"
    cache_dir.mkdir(parents=True, exist_ok=True)
    for name, result in all_results.items():
        out = cache_dir / f"{name.split('_')[0]}-result-{now.strftime('%Y-%m-%d-%H-%S')}.json"
        with out.open("w", encoding="utf-8") as fh:
            json.dump(result, fh, indent=4)

    for name, results in all_results.items():
        failed = []
        for ret in results[:-1]:
            is_failed = False
            for metric in metrics.keys():
                if metric in suppression["evaluators"]:
                    continue
                if metric == "groundedness":
                    if ret.get("groundedness_result") == "fail":
                        is_failed = True
                elif ret.get(f"{metric}_result") not in (None, "pass"):
                    is_failed = True
            if is_failed:
                failed.append(ret)
                logging.info("test case: %s - Failed", ret.get("testcase"))
        if failed:
            out = cache_dir / f"{name.split('_')[0]}-failed-cases-{now.strftime('%Y-%m-%d-%H-%S')}.json"
            with out.open("w", encoding="utf-8") as fh:
                json.dump(failed, fh, indent=4)


if __name__ == "__main__":
    sys.exit(main())
