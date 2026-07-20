"""Coded, **benchmark-gated** self-evolution workflow for the `azure-typespec-author` skill.

This is the **"run it as code"** orchestrator (the Python counterpart of
`.github/skills/azure-typespec-author/agentic-doc-refinement/doc-refinement.mjs`),
adapted for the Foundry hosted agent so it can run **online (in the container) or
locally** with the same code path. The reasoning steps are agent turns; the eval
step is a deterministic Azure DevOps pipeline trigger and the PR gate is a
deterministic pass-rate check.

Order (PR is opened LAST, only if the benchmark clears the threshold):
  1. Agent analyzes an Excel of user prompts + updates the reference/skill docs,
     then **pushes them to a branch (commit, NO PR yet)**   -> push_skill_changes
  2. Run the Vally code-quality (forced) evals on that branch -> ADO pipeline 8178
  3. Compute the benchmark pass rate                          -> compute_pass_rate
  4. Agent analyzes results / attributes gaps                 -> from the artifact
  5. Agent generates the gap report                           -> the PR body
  GATE. If pass_rate > --min-pass-rate (default 75%), open a **draft** PR carrying
        the report; otherwise skip the PR and log why.

Authentication:
  * GitHub  — PAT / GitHub App via `tools/github_tools.py` (`GITHUB_TOKEN` from `.env`).
  * ADO     — PAT / AAD token via `tools/ado_pipeline_tools.py` (`ADO_PAT` / `az login`).
  * Foundry — `DefaultAzureCredential` (same as `init.py`).

Usage (from the `agent/` folder, with `.env` populated):
  python workflow.py --project-endpoint <url> --model <deployment> \
      --prompts-excel ./user-prompts.xlsx --min-pass-rate 75
  python workflow.py --skip-docs --branch self-evolve/paging   # rerun benchmark+gate
  python workflow.py --skip-eval --results-file ./results.jsonl --branch self-evolve/paging
"""

from __future__ import annotations

import argparse
import asyncio
import json
import logging
import os
import sys
from pathlib import Path

import yaml
from dotenv import load_dotenv

_AGENT_DIR = Path(__file__).resolve().parent
if str(_AGENT_DIR) not in sys.path:
    sys.path.insert(0, str(_AGENT_DIR))

load_dotenv(_AGENT_DIR / ".env", override=False)
load_dotenv(override=False)

from init import build_agent  # noqa: E402  (after sys.path + dotenv)
from tools import github_tools  # noqa: E402
from tools.skill_evolution_tools import (  # noqa: E402
    compute_pass_rate,
    open_draft_pr_if_benchmark_passed,
    read_prompt_excel,
)
from tools.ado_pipeline_tools import (  # noqa: E402
    trigger_pipeline_run,
    wait_for_pipeline_run,
    download_pipeline_results,
)

logger = logging.getLogger("workflow")

_PROMPTS_DIR = _AGENT_DIR / "workflow_prompts"
_DEFAULT_PIPELINE_ID = 8178
_DEFAULT_PROJECT = "internal"
_DEFAULT_MIN_PASS_RATE = 75.0
# Cap how much of the Excel we inline into the step-1 prompt.
_MAX_EXCEL_PROMPT_ROWS = 300


def _read_prompt(name: str) -> str:
    return (_PROMPTS_DIR / name).read_text(encoding="utf-8")


def _agent_text(response) -> str:
    """Extract the text of an AgentRunResponse robustly."""
    text = getattr(response, "text", None)
    if text:
        return text
    messages = getattr(response, "messages", None)
    if messages:
        return "\n".join(str(getattr(m, "text", m)) for m in messages)
    return str(response)


async def _run_turn(agent, prompt: str) -> str:
    """Run one autonomous agent turn and return its text."""
    response = await agent.run(prompt)
    return _agent_text(response)


def _build_excel_context(source: str) -> str:
    """Read the user-prompt Excel and format it for the step-1 prompt.

    Returns an empty string (and logs a warning) if the workbook cannot be read —
    the workflow proceeds without it rather than aborting, since the SharePoint
    source is auth-gated and may be unavailable to a remote agent.
    """
    raw = read_prompt_excel(source, max_rows=_MAX_EXCEL_PROMPT_ROWS)
    try:
        data = json.loads(raw)
    except json.JSONDecodeError:
        logger.warning("read_prompt_excel returned non-JSON: %s", raw[:200])
        return ""
    if data.get("status") != "ok":
        logger.warning("Could not read prompts Excel '%s': %s", source, data.get("message"))
        return ""
    rows = data.get("rows", [])
    logger.info("Loaded %d user prompts from '%s' (column=%s).",
                len(rows), source, data.get("column"))
    if not rows:
        return ""
    listing = "\n".join(f"- {r}" for r in rows)
    return (
        "\n\n## User telemetry to analyze (from the provided Excel)\n\n"
        "Analyze the real user prompts below, cluster them into **common use cases**, "
        "and map each cluster onto the skill's case categories to decide which "
        "reference-document links to add or update in "
        "`references/reference-document-links.md` (the primary target). "
        "A use case counts as **common only if similar prompts appear at least 5 times** "
        "in the telemetry below; **only add/update a reference-document link for a common "
        "case when it is not already covered** by the reference doc. Ignore rare/one-off "
        "prompts (fewer than 5 similar occurrences) and cases already covered — if nothing "
        "clears the threshold and is uncovered, change nothing. "
        "AVOID editing "
        "other skill markdown files unless it is genuinely needed and necessary, then "
        "keep changes minimal:\n\n"
        f"{listing}\n"
    )


async def run_workflow(args: argparse.Namespace) -> int:
    with open(_AGENT_DIR / "agent.yaml", encoding="utf-8") as f:
        agent_cfg = yaml.safe_load(f)
    agent_name = agent_cfg["name"]

    agent = build_agent(
        project_endpoint=args.project_endpoint,
        model=args.model,
        reasoning_effort=args.reasoning_effort,
        agent_name=agent_name,
        agent_id=agent_name,
    )

    branch = args.branch

    # ---- Step 1: analyze prompts, update docs+skill, PUSH a branch (no PR) --
    if not args.skip_docs:
        logger.info("Step 1 — analyze user telemetry, update reference-document-links.md "
                    "(+ skill if needed), push a branch")
        prompt = _read_prompt("01-update-skill.md")
        if args.prompts_excel:
            prompt += _build_excel_context(args.prompts_excel)
        before = len(github_tools.LAST_BRANCH_PUSHES)
        out = await _run_turn(agent, prompt)
        logger.info("Agent (step 1) said:\n%s", out[:2000])
        if len(github_tools.LAST_BRANCH_PUSHES) > before:
            push = github_tools.LAST_BRANCH_PUSHES[-1]
            branch = push["branch"]
            logger.info("Pushed %d file(s) to branch '%s' (no PR yet).",
                        len(push.get("files_committed", [])), branch)
        else:
            logger.error("Step 1 did not push a branch (push_skill_changes was not "
                         "called or failed). Aborting.")
            return 2
    else:
        logger.info("Skipping step 1 (--skip-docs). Reusing branch '%s'.", branch)
        if not branch:
            logger.error("--skip-docs requires --branch.")
            return 2

    # ---- Step 2: run the ADO code-quality benchmark on the pushed branch ----
    results_content = ""
    if not args.skip_eval:
        logger.info("Step 2 — trigger ADO pipeline %s on '%s'", args.pipeline_id, branch)
        run = trigger_pipeline_run(
            branch=branch, pipeline_id=args.pipeline_id, project=args.project
        )
        if run.get("status") != "ok":
            logger.error("Failed to trigger ADO pipeline: %s", run.get("message"))
            return 3
        run_id = run["run_id"]
        logger.info("Queued ADO run %s: %s", run_id, run.get("web_url"))
        final = wait_for_pipeline_run(
            run_id, pipeline_id=args.pipeline_id, project=args.project,
            timeout_secs=args.eval_timeout,
        )
        logger.info("ADO run %s finished: state=%s result=%s",
                    run_id, final.get("state"), final.get("result"))
        dl = download_pipeline_results(run_id, project=args.project)
        if dl.get("status") == "ok" and dl.get("found"):
            results_content = dl["content"]
        else:
            logger.warning("Could not download results artifact: %s", dl.get("message"))
    elif args.results_file:
        logger.info("Skipping step 2; reading results from %s", args.results_file)
        results_content = Path(args.results_file).read_text(encoding="utf-8")
    else:
        logger.info("Skipping step 2 (--skip-eval) with no --results-file; "
                    "no eval data available.")

    # ---- Step 3: compute the deterministic pass-rate gate value ------------
    pass_rate = None
    if results_content:
        summary = json.loads(compute_pass_rate(content=results_content))
        if summary.get("status") == "ok":
            pass_rate = summary["pass_rate"]
            logger.info("Step 3 — benchmark: %s/%s passed, pass_rate=%.1f%% (threshold %.1f%%)",
                        summary["passed"], summary["cases"], pass_rate, args.min_pass_rate)
        else:
            logger.warning("Could not compute pass rate: %s", summary.get("message"))
    else:
        logger.warning("Step 3 — no benchmark results; pass rate is unknown.")

    # ---- Step 4: analyze results -------------------------------------------
    logger.info("Step 4 — analyze eval results")
    analysis_prompt = _read_prompt("04-analyze-results.md")
    if results_content:
        analysis_prompt += f"\n\n## Pipeline artifact content\n\n{results_content}"
    else:
        analysis_prompt += ("\n\n(No artifact content was available — state that results "
                            "could not be retrieved and analyze only what you can infer.)")
    analysis = await _run_turn(agent, analysis_prompt)
    logger.info("Analysis (step 4):\n%s", analysis[:2000])

    # ---- Step 5: generate the gap report (becomes the PR body) -------------
    logger.info("Step 5 — generate the documentation-gap report")
    report_prompt = _read_prompt("05-generate-report.md") + (
        f"\n\n## Benchmark pass rate\n\n"
        f"{'%.1f%%' % pass_rate if pass_rate is not None else 'unknown'} "
        f"(threshold {args.min_pass_rate:.1f}%).\n\n"
        f"## Step-4 analysis to turn into the report\n\n{analysis}"
    )
    report = await _run_turn(agent, report_prompt)
    logger.info("Report (step 5):\n%s", report[:2000])

    # ---- GATE: open a DRAFT PR only if the benchmark cleared the threshold --
    if pass_rate is None:
        logger.error("Benchmark pass rate is unknown — NOT opening a PR. Report below.\n%s", report)
        return 4

    title = f"[self-evolve] Update azure-typespec-author references (benchmark {pass_rate:.1f}%)"
    body = (
        f"Automated by the Self-Evolving Agent. Benchmark pass rate "
        f"**{pass_rate:.1f}%** (> {args.min_pass_rate:.1f}% threshold).\n\n{report}"
    )
    # Single source of truth for the gate: open_draft_pr_if_benchmark_passed opens
    # a draft PR iff pass_rate > min_pass_rate, otherwise returns 'skipped'.
    gate_raw = open_draft_pr_if_benchmark_passed(
        branch=branch, title=title, body=body,
        content=results_content, min_pass_rate=args.min_pass_rate,
    )
    try:
        gate = json.loads(gate_raw)
    except json.JSONDecodeError:
        gate = {"status": "error", "message": gate_raw}

    if gate.get("status") == "skipped":
        logger.warning("Pass rate %.1f%% did not exceed the %.1f%% threshold — "
                       "NOT opening a PR. Report below.\n%s",
                       pass_rate, args.min_pass_rate, report)
        return 5
    if gate.get("status") != "ok":
        logger.error("Failed to open the draft PR: %s", gate.get("message"))
        return 6

    logger.info("Pass rate %.1f%% cleared the %.1f%% threshold — draft PR #%s opened "
                "(draft=%s): %s",
                pass_rate, args.min_pass_rate, gate.get("pr_number"),
                gate.get("draft"), gate.get("html_url"))
    return 0


def _parse_args(argv: list[str]) -> argparse.Namespace:
    p = argparse.ArgumentParser(description="Coded, benchmark-gated self-evolution workflow.")
    p.add_argument("--project-endpoint",
                   default=os.environ.get("AI_FOUNDRY_PROJECT_ENDPOINT"),
                   help="Foundry project endpoint (env AI_FOUNDRY_PROJECT_ENDPOINT).")
    p.add_argument("--model",
                   default=os.environ.get("AI_FOUNDRY_AGENT_MODEL"),
                   help="Model deployment name (env AI_FOUNDRY_AGENT_MODEL).")
    p.add_argument("--reasoning-effort",
                   default=os.environ.get("AI_FOUNDRY_AGENT_REASONING_EFFORT", "medium"))
    p.add_argument("--prompts-excel", default=os.environ.get("PROMPTS_EXCEL"),
                   help="Local path or direct-download URL of the user-prompts .xlsx "
                        "(SharePoint sharing links must be downloaded first). env PROMPTS_EXCEL.")
    p.add_argument("--min-pass-rate", type=float, default=_DEFAULT_MIN_PASS_RATE,
                   help="Benchmark pass-rate percentage the PR must exceed (default 75).")
    p.add_argument("--pipeline-id", type=int, default=_DEFAULT_PIPELINE_ID,
                   help="ADO pipeline/definition id for the benchmark (default 8178).")
    p.add_argument("--project", default=_DEFAULT_PROJECT, help="ADO project (default 'internal').")
    p.add_argument("--eval-timeout", type=int, default=3600,
                   help="Seconds to wait for the ADO run to complete (default 3600).")
    p.add_argument("--skip-docs", action="store_true",
                   help="Skip step 1; reuse an already-pushed branch (needs --branch).")
    p.add_argument("--skip-eval", action="store_true",
                   help="Skip step 2; optionally analyze --results-file instead.")
    p.add_argument("--results-file", default=None,
                   help="With --skip-eval: a local results.jsonl to feed steps 3-5.")
    p.add_argument("--branch", default=None,
                   help="Existing head branch (with --skip-docs) to benchmark and open the PR from.")
    args = p.parse_args(argv)
    if not args.project_endpoint or not args.model:
        p.error("--project-endpoint and --model are required "
                "(or set AI_FOUNDRY_PROJECT_ENDPOINT / AI_FOUNDRY_AGENT_MODEL).")
    return args


def main() -> None:
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s %(levelname)s %(name)s: %(message)s",
        stream=sys.stdout,
    )
    for noisy in ("azure.core.pipeline.policies.http_logging_policy", "httpx", "uvicorn"):
        logging.getLogger(noisy).setLevel(logging.WARNING)
    args = _parse_args(sys.argv[1:])
    raise SystemExit(asyncio.run(run_workflow(args)))


if __name__ == "__main__":
    main()
