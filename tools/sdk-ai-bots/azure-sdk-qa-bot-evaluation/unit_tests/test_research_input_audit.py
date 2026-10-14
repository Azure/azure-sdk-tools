import json
from pathlib import Path
import sys
from types import SimpleNamespace

import pytest

from research import audit_inputs

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "azure-sdk-qa-bot-agent"))
from models.knowledge import DocumentContext
from services.decision_context import DecisionContext, DecisionEvidence


def fixture(mode="quoted"):
    prepared = DecisionContext(mode, (
        DecisionEvidence("source", "documentation", DocumentContext(
            document_content="The actual source text provides a daily schedule.",
        )),
    ))
    raw = json.dumps({
        "claims": [{
            "kind": "policy", "text": "The schedule is daily.",
            "citations": [{"source_id": "source", "quote": "provides a daily schedule"}],
            "condition": "",
        }], "missing_facts": [],
    })
    inputs = [
        {"role": "user", "content": "My question"},
        {"role": "user", "content": prepared.input_text()},
        {"role": "system", "content": prepared.instruction()},
    ]
    row = {
        "testcase": "case", "query": "My question",
        "actual": {
            "answer": prepared.render(raw),
            "context": json.dumps([item.document.model_dump(mode="json") for item in prepared.evidence]),
        },
        "execution": {"response_id": "actual-id"},
    }
    return row, inputs, SimpleNamespace(id="actual-id", output_text=raw, output=[])


def test_actual_input_roles_and_rendering():
    row, inputs, response = fixture()
    result = audit_inputs.audit_record(row, inputs, response, "quoted")
    assert result["actual_answer_rendering_verified"]
    assert result["model_input_roles_verified"]


def test_system_contract_cannot_be_delivered_as_user_context():
    row, inputs, response = fixture()
    inputs[-1]["role"] = "user"
    with pytest.raises(ValueError, match="system inputs"):
        audit_inputs.audit_record(row, inputs, response, "quoted")


def test_returned_evidence_cannot_be_replaced_with_oracle():
    row, inputs, response = fixture()
    row["actual"]["context"] = json.dumps([{"document_content": "Canonical oracle"}])
    with pytest.raises(ValueError, match="missing or altered"):
        audit_inputs.audit_record(row, inputs, response, "quoted")


def test_rendered_answer_cannot_be_changed_after_generation():
    row, inputs, response = fixture()
    row["actual"]["answer"] = "My preferred answer"
    with pytest.raises(ValueError, match="rendering"):
        audit_inputs.audit_record(row, inputs, response, "quoted")


def test_baseline_cannot_receive_decision_evidence():
    row, inputs, response = fixture()
    with pytest.raises(ValueError, match="Baseline unexpectedly"):
        audit_inputs.audit_record(row, inputs, response, "")


def test_actual_response_identity_is_required():
    row, inputs, response = fixture()
    row["execution"]["response_id"] = "different-id"
    with pytest.raises(ValueError, match="identity"):
        audit_inputs.audit_record(row, inputs, response, "quoted")


def test_extra_oracle_context_cannot_be_appended():
    row, inputs, response = fixture()
    docs = json.loads(row["actual"]["context"])
    docs.append({"document_content": "Injected canonical oracle"})
    row["actual"]["context"] = json.dumps(docs)
    with pytest.raises(ValueError, match="differs from actual"):
        audit_inputs.audit_record(row, inputs, response, "quoted")


def test_question_uses_the_same_normalization_as_actual_chat():
    row, inputs, response = fixture()
    row["query"] = "My Swagger question"
    inputs[0]["content"] = "My Open API question"
    assert audit_inputs.audit_record(row, inputs, response, "quoted")[
        "actual_answer_rendering_verified"
    ]
