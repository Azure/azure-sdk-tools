"""Best-effort telemetry hook: report one azure-typespec-author session to the
hosted **feedback agent**.

This is the client side of the self-evolution loop. The skill (or any caller)
runs it once at the end of an authoring task. It sends a compact, anonymized
telemetry payload to the deployed ``typespec-authoring-skill-feedback-agent``,
whose LLM calls ``record_session_telemetry`` to persist it to Application
Insights.

Design constraints (important — this runs at the tail of a user's task):
  * **Non-blocking / best-effort** — never raises to the caller and always exits
    0. Telemetry must never fail an authoring task or surface an error to the
    user. A short timeout guards against a cold container start.
  * **Anonymized** — pass only the (already anonymized) prompt gist and a few
    structured signals. Do not pass secrets, file contents, or PII.
  * **Auth** — uses AzureCliCredential, so the caller must be ``az login``-ed and
    have access to the Foundry project. In environments without that, the hook
    degrades to a no-op (prints a skip notice, exits 0).

Usage (from the skill's Step 7 or manually):

    python send_feedback.py \
        --prompt "add a versioning enum to my service" \
        --outcome success \
        --skill-triggered true \
        --asked-clarifying-questions false \
        --tool-call-errors 0 \
        --retries 0 \
        --feedback "worked well" \
        --session-id abc123

Every flag except ``--prompt``/``--outcome`` is optional. Override the target with
``--project-endpoint`` / ``--agent-name`` or the matching env vars.
"""

from __future__ import annotations

import argparse
import json
import os
import sys

DEFAULT_ENDPOINT = os.environ.get(
    "FEEDBACK_PROJECT_ENDPOINT",
    "https://foundry-haoling-eus2.services.ai.azure.com/api/projects/proj-default",
)
DEFAULT_AGENT_NAME = os.environ.get(
    "FEEDBACK_AGENT_NAME", "typespec-authoring-skill-feedback-agent"
)
DEFAULT_TIMEOUT_S = float(os.environ.get("FEEDBACK_TIMEOUT_S", "45"))


def _to_bool(value: str) -> bool:
    return str(value).strip().lower() in {"1", "true", "yes", "y", "success", "on"}


def _parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser(description="Report one session to the feedback agent.")
    p.add_argument("--prompt", required=True, help="Anonymized user prompt / request gist.")
    p.add_argument("--outcome", required=True, help="success | failure | partial")
    p.add_argument("--skill-triggered", default="true")
    p.add_argument("--asked-clarifying-questions", default="false")
    p.add_argument("--tool-call-errors", type=int, default=0)
    p.add_argument("--retries", type=int, default=0)
    p.add_argument("--feedback", default=None)
    p.add_argument("--session-id", default=None)
    p.add_argument("--project-endpoint", default=DEFAULT_ENDPOINT)
    p.add_argument("--agent-name", default=DEFAULT_AGENT_NAME)
    p.add_argument("--timeout", type=float, default=DEFAULT_TIMEOUT_S)
    return p.parse_args()


def _build_message(args: argparse.Namespace) -> str:
    """A compact, explicit instruction so the agent reliably records exactly one
    telemetry record with these fields (no clarifying questions)."""
    payload = {
        "user_prompt": args.prompt,
        "outcome": args.outcome,
        "skill_triggered": _to_bool(args.skill_triggered),
        "asked_clarifying_questions": _to_bool(args.asked_clarifying_questions),
        "tool_call_errors": int(args.tool_call_errors),
        "retries": int(args.retries),
        "feedback": args.feedback,
        "session_id": args.session_id,
    }
    return (
        "Record this azure-typespec-author session telemetry now by calling "
        "record_session_telemetry exactly once with these fields, then briefly "
        "acknowledge. Do not ask any clarifying questions. Fields (JSON):\n"
        + json.dumps(payload, ensure_ascii=False)
    )


def main() -> int:
    args = _parse_args()
    try:
        from azure.ai.projects import AIProjectClient
        from azure.identity import AzureCliCredential
    except Exception as exc:  # deps not installed -> no-op
        print(f"[feedback] skipped (deps unavailable: {exc}); exiting 0")
        return 0

    project = None
    try:
        project = AIProjectClient(
            endpoint=args.project_endpoint,
            credential=AzureCliCredential(),
            allow_preview=True,
        )
        client = project.get_openai_client(agent_name=args.agent_name)
        resp = client.responses.create(
            input=_build_message(args),
            store=True,
            timeout=args.timeout,
        )
        print("[feedback] recorded:", getattr(resp, "output_text", "ok"))
        return 0
    except Exception as exc:  # best-effort: never fail the caller
        print(f"[feedback] skipped (non-fatal: {type(exc).__name__}: {str(exc)[:200]})")
        return 0
    finally:
        if project is not None:
            try:
                project.close()
            except Exception:
                pass


if __name__ == "__main__":
    # Always exit 0 — telemetry must never break an authoring task.
    try:
        sys.exit(main())
    except SystemExit:
        raise
    except Exception as exc:  # absolute safety net
        print(f"[feedback] skipped (guarded: {exc})")
        sys.exit(0)
