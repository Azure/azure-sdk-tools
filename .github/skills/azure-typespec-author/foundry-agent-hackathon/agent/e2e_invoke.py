"""Online e2e smoke test: prompt the deployed Foundry hosted agent.

Sends a single prompt to the hosted `typespec-authoring-skill-self-evolving-agent`
via the OpenAI Responses API (agent_reference) and prints the streamed output.

Usage:
  python e2e_invoke.py --project-endpoint <url> [--prompt "..."] [--timeout 3600]
"""
from __future__ import annotations

import argparse
import sys

# Windows consoles default to cp1252; the agent streams UTF-8 (incl. private-use
# citation markers like U+E200). Force UTF-8 with replacement so streaming never
# crashes the harness on an unencodable character.
for _stream in (sys.stdout, sys.stderr):
    try:
        _stream.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

from azure.ai.projects import AIProjectClient
from azure.identity import AzureCliCredential

_AGENT_NAME = "typespec-authoring-skill-self-evolving-agent"

_DEFAULT_PROMPT = (
    "Start the self-improvement workflow for the `azure-typespec-author` skill.\n"
    "{excel_line}"
    "Analyze the telemetry to find common use cases, update the skill (mainly "
    "references/reference-document-links.md, other skill files only if truly necessary), "
    "and push the change to a branch on the upstream Azure/azure-sdk-tools repo. "
    "Then run the ADO code-quality benchmark (pipeline 8178) on that branch, produce a "
    "benchmark test report, and — only if the pass rate exceeds 75% — open a draft PR "
    "carrying the report. Follow your standard procedure. End your answer with a Links "
    "section containing the ADO benchmark run link and the draft PR link (or why it was "
    "not opened)."
)


def _build_prompt(prompt: str | None, excel: str | None) -> str:
    if prompt:
        return prompt
    excel_line = (
        f"Telemetry Excel link: {excel}\nRead it with read_prompt_excel(source=<that link>). "
        if excel
        else ""
    )
    return _DEFAULT_PROMPT.format(excel_line=excel_line)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--project-endpoint", required=True)
    parser.add_argument("--prompt", default=None)
    parser.add_argument(
        "--excel",
        default=None,
        help="Link (or path) to the telemetry Excel workbook the agent should analyze.",
    )
    args = parser.parse_args()
    prompt = _build_prompt(args.prompt, args.excel)

    project = AIProjectClient(
        endpoint=args.project_endpoint,
        credential=AzureCliCredential(),
        allow_preview=True,
    )

    info = project.agents.get(_AGENT_NAME).as_dict()
    latest = info.get("versions", {}).get("latest", {})
    version = latest.get("version")
    status = latest.get("status")
    print(f"Invoking {_AGENT_NAME} v{version} (status={status})", flush=True)

    openai_client = project.get_openai_client(agent_name=_AGENT_NAME)
    agent_ref = {
        "name": _AGENT_NAME,
        "version": str(version),
        "type": "agent_reference",
    }

    conversation = openai_client.conversations.create()
    conv_id = conversation.id
    print(f"conversation={conv_id}", flush=True)

    def _run_turn(inp):
        """Stream one Responses turn on the shared conversation."""
        stream = openai_client.responses.create(
            input=inp,
            store=True,
            stream=True,
            conversation=conv_id,
            extra_body={"agent_reference": agent_ref},
        )
        final = None
        resp_id = None
        produced = False
        counts: dict[str, int] = {}
        for event in stream:
            etype = getattr(event, "type", "")
            counts[etype] = counts.get(etype, 0) + 1
            ev_resp = getattr(event, "response", None)
            if ev_resp is not None and getattr(ev_resp, "id", None):
                resp_id = ev_resp.id
            if etype == "response.output_text.delta":
                produced = True
                sys.stdout.write(getattr(event, "delta", ""))
                sys.stdout.flush()
            elif etype == "response.completed":
                final = ev_resp
            elif etype and ("error" in etype or "failed" in etype):
                print(f"\n[EVENT] {etype}: {event}", flush=True)
        ncalls = counts.get("response.function_call_arguments.done", 0)
        print(f"\n[turn done] resp_id={resp_id} tool_calls={ncalls} "
              f"text_deltas={counts.get('response.output_text.delta', 0)}", flush=True)
        return final, produced

    # First turn.
    final, produced = _run_turn(prompt)

    # Continue the same conversation until the agent emits a textual report
    # (it may spend early turns entirely on tool calls before summarizing).
    nudge = (
        "Continue where you left off on the SAME task. Keep going through your "
        "procedure (push the branch to upstream Azure/azure-sdk-tools, trigger the ADO "
        "benchmark pipeline 8178 and capture its run link, wait for it, generate the "
        "benchmark test report, and open the draft PR only if the pass rate exceeds "
        "75%). When you are done, output the final benchmark test report as your text "
        "answer in Markdown, ending with a Links section that has the ADO benchmark run "
        "link and the draft PR link (or why it was not opened)."
    )
    max_continuations = 8
    while not produced and max_continuations > 0:
        max_continuations -= 1
        print(f"\n----- continuing conversation {conv_id} -----", flush=True)
        final, produced = _run_turn(nudge)

    print("\n===== FINAL =====", flush=True)
    if final is not None:
        print(f"id={getattr(final, 'id', '?')} status={getattr(final, 'status', '?')}", flush=True)
        err = getattr(final, "error", None)
        if err:
            print(f"error={err}", flush=True)
        print("--- output_text ---", flush=True)
        print(getattr(final, "output_text", "") or "(empty)", flush=True)
    else:
        print("(no response captured)", flush=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
