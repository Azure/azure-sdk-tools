"""One-turn probe: dump every output item type + any message/refusal content."""
import argparse
import json
import sys

from azure.ai.projects import AIProjectClient
from azure.identity import AzureCliCredential

_AGENT_NAME = "typespec-authoring-skill-self-evolving-agent"


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--project-endpoint", required=True)
    ap.add_argument("--prompt", default="In one short paragraph, say hello and confirm you can respond with text.")
    args = ap.parse_args()

    project = AIProjectClient(endpoint=args.project_endpoint, credential=AzureCliCredential(), allow_preview=True)
    info = project.agents.get(_AGENT_NAME).as_dict()
    version = info.get("versions", {}).get("latest", {}).get("version")
    print(f"agent v{version}", flush=True)
    c = project.get_openai_client(agent_name=_AGENT_NAME)
    agent_ref = {"name": _AGENT_NAME, "version": str(version), "type": "agent_reference"}

    stream = c.responses.create(
        input=args.prompt, store=True, stream=True,
        extra_body={"agent_reference": agent_ref},
    )
    final = None
    for ev in stream:
        t = getattr(ev, "type", "")
        if t == "response.output_text.delta":
            sys.stdout.write(getattr(ev, "delta", "")); sys.stdout.flush()
        elif t == "response.completed":
            final = getattr(ev, "response", None)
        elif t == "response.output_item.done":
            item = getattr(ev, "item", None)
            it = getattr(item, "type", None) if item else None
            if it in ("message", "refusal"):
                try:
                    print("\nITEM:", json.dumps(item.model_dump(), default=str)[:1500], flush=True)
                except Exception:
                    print("\nITEM(raw):", str(item)[:1500], flush=True)

    print("\n\n== FINAL from stream ==", flush=True)
    if final is not None:
        d = final.model_dump()
        print("status:", d.get("status"), "| output_text:", repr(getattr(final, "output_text", ""))[:200], flush=True)
        for it in (d.get("output") or []):
            print(" item.type:", it.get("type"),
                  "| content:", str(it.get("content"))[:200] if it.get("content") else "", flush=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
