"""Audit recorded responses with the same authorized identity as the CI server."""

import argparse
import asyncio
import hashlib
import json
from pathlib import Path
import sys

AGENT = Path(__file__).resolve().parents[2] / "azure-sdk-qa-bot-agent"


def strings(value):
    if isinstance(value, str):
        yield value
    elif isinstance(value, dict):
        for child in value.values():
            yield from strings(child)
    elif isinstance(value, list):
        for child in value:
            yield from strings(child)


def evidence_packet(inputs):
    prefix = "[decision_evidence]\n"
    packets = [
        text for item in inputs if item.get("role") == "user"
        for text in strings(item) if text.startswith(prefix)
    ]
    if len(packets) != 1:
        raise ValueError(f"Expected one decision-evidence input, found {len(packets)}")
    return json.loads(packets[0][len(prefix):])


def audit_record(row, inputs, raw_response, mode):
    from models.knowledge import DocumentContext, SearchKnowledgeBaseResult
    from services.chat_service import ChatService, _CITATION_RE
    from services.decision_context import DecisionContext, DecisionEvidence
    from utils.text_util import preprocess_message

    query = row["query"]
    if not any(
        preprocess_message(query) == text for item in inputs if item.get("role") == "user"
        for text in strings(item)
    ):
        raise ValueError("Recorded question does not match actual response input")
    if raw_response.id != row["execution"].get("response_id"):
        raise ValueError("Recorded response identity mismatch")
    returned = json.loads(row["actual"].get("context") or "[]")
    tools = ChatService()._extract_tool_results(raw_response.output)
    expected_documents = [
        DocumentContext.from_reference(reference).model_dump(mode="json")
        for name in ("search_knowledge_base", "wiki_search")
        if isinstance(search := tools.get(name), SearchKnowledgeBaseResult)
        for reference in search.results
    ]
    if mode:
        packet = evidence_packet(inputs)
        prepared = DecisionContext(mode, tuple(
            DecisionEvidence(
                source_id=item["source_id"], role=item["role"],
                document=DocumentContext.model_validate(item),
            )
            for item in packet
        ))
        if mode != "evidence_only":
            if not any(
                prepared.instruction() == text
                for item in inputs if item.get("role") == "system"
                for text in strings(item)
            ):
                raise ValueError("First-pass decision contract not found in system inputs")
            rendered = prepared.render(raw_response.output_text)
        else:
            rendered = raw_response.output_text
        for item in prepared.evidence:
            if item.document.model_dump(mode="json") not in returned:
                raise ValueError("Actual input evidence missing or altered in returned context")
        expected_documents.extend(item.document.model_dump(mode="json") for item in prepared.evidence)
    else:
        if any("[decision_evidence]" in text for item in inputs for text in strings(item)):
            raise ValueError("Baseline unexpectedly received decision-contract evidence")
        rendered = raw_response.output_text
    if returned != expected_documents:
        raise ValueError("Returned context differs from actual tool outputs and input evidence")
    rendered = _CITATION_RE.sub("", rendered)
    answer, _ = ChatService._extract_references_from_text(rendered)
    if answer != row["actual"]["answer"]:
        raise ValueError("Recorded answer does not match actual response rendering")
    return {
        "testcase": row["testcase"], "response_id": raw_response.id,
        "model_input_roles_verified": True, "actual_answer_rendering_verified": True,
        "actual_evidence_delivery_verified": bool(mode),
        "answer_sha256": hashlib.sha256(answer.encode()).hexdigest(),
    }


async def run(args):
    sys.path.insert(0, str(AGENT))
    import config.app_config as app_config
    from utils.azure_ai_foundry import get_openai_client, close_clients
    from utils.azure_credential import close_credential
    from services.decision_context import validate_mode

    validate_mode(args.mode)
    await app_config.init()
    client = get_openai_client("azure-sdk-chat-agent")
    records = []
    errors = []
    files = list(args.cache.glob("*-result-*.json"))
    if not files:
        raise ValueError("No full evaluation cache files found")
    try:
        for path in files:
            cache = json.loads(path.read_text(encoding="utf-8"))
            for group in cache.values():
                for row in group[:-1]:
                    if not row.get("execution", {}).get("response_id"):
                        errors.append({"testcase": row.get("testcase"), "error": "No actual response identity"})
                        continue
                    response_id = row["execution"]["response_id"]
                    inputs = [
                        item.model_dump(mode="json")
                        async for item in client.responses.input_items.list(response_id)
                    ]
                    raw_response = await client.responses.retrieve(response_id)
                    records.append(audit_record(row, inputs, raw_response, args.mode))
    finally:
        await close_clients()
        await close_credential()
    report = {"mode": args.mode, "rows": records, "errors": errors, "passed": not errors}
    (args.cache / "owning-identity-input-audit.json").write_text(
        json.dumps(report, indent=2), encoding="utf-8",
    )
    if errors:
        raise ValueError(f"Input audit failed for {len(errors)} recorded rows")
    print(f"Verified actual response inputs, evidence and rendering for {len(records)} rows.")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--cache", type=Path, required=True)
    parser.add_argument("--mode", default="")
    asyncio.run(run(parser.parse_args()))


if __name__ == "__main__":
    main()
