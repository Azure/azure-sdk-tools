"""Opt-in, first-pass evidence-contract experiments for API-spec review."""

from __future__ import annotations

from dataclasses import dataclass
import hashlib
import json
import logging
from typing import Literal

from pydantic import BaseModel, ConfigDict, Field

from config.tenant_config import TenantID
from models.chat import ChatRequest
from models.knowledge import DocumentContext
from models.knowledge_retrieve import KnowledgeRetrieveRequest
from services.knowledge_service import KnowledgeService

logger = logging.getLogger(__name__)

MODES = frozenset({"", "evidence_only", "quoted", "role_bound", "extractive"})


class EvidenceCitation(BaseModel):
    model_config = ConfigDict(extra="forbid")

    source_id: str
    quote: str = Field(min_length=12)


class DecisionClaim(BaseModel):
    model_config = ConfigDict(extra="forbid")

    kind: Literal[
        "policy", "case_fact", "execution_outcome", "authorization_outcome",
        "recommendation", "question",
    ]
    text: str = Field(min_length=1)
    citations: list[EvidenceCitation]
    condition: str


class DecisionAnswer(BaseModel):
    model_config = ConfigDict(extra="forbid")

    claims: list[DecisionClaim] = Field(min_length=1, max_length=12)
    missing_facts: list[str] = Field(max_length=2)


@dataclass(frozen=True)
class DecisionEvidence:
    source_id: str
    role: Literal["documentation", "request_context"]
    document: DocumentContext


@dataclass(frozen=True)
class DecisionContext:
    mode: str
    evidence: tuple[DecisionEvidence, ...]

    def input_text(self) -> str:
        return "[decision_evidence]\n" + json.dumps([
            {
                "source_id": item.source_id,
                "role": item.role,
                **item.document.model_dump(mode="json"),
            }
            for item in self.evidence
        ], ensure_ascii=False)

    def instruction(self) -> str:
        return (
            "Return one JSON object matching DecisionAnswer, not Markdown. "
            "Separate every material fact or action into a claim. Cite exact, "
            "contiguous quotations and source_id values from decision_evidence; "
            "do not invent sources. Documentation describes policy or mechanics, "
            "not execution of a particular run or an actual review approval. "
            "request_context records the user's account, not independently "
            "verified events. Classify claims honestly: policy, case_fact, "
            "execution_outcome, authorization_outcome, recommendation, question. "
            "For a recommendation dependent on unknown circumstances, put those "
            "circumstances in condition; otherwise condition is an empty string. "
            "Ask for at most two decision-changing missing facts. Do not promise "
            "future outcomes or infer approval from documented requirements. "
            "Use this schema:\n" + json.dumps(DecisionAnswer.model_json_schema())
        )

    def render(self, text: str) -> str:
        answer = DecisionAnswer.model_validate_json(text)
        sources = {item.source_id: item for item in self.evidence}
        output: list[str] = []
        blocked = 0
        for claim in answer.claims:
            if claim.kind == "question":
                # The contract requires an explicit question form, not an assertion.
                if not claim.text.rstrip().endswith("?"):
                    blocked += 1
                    continue
                output.append(claim.text)
                continue
            cited: list[DecisionEvidence] = []
            for citation in claim.citations:
                source = sources.get(citation.source_id)
                if source is None or citation.quote not in source.document.document_content:
                    break
                cited.append(source)
            if not cited or len(cited) != len(claim.citations):
                blocked += 1
                continue
            if self.mode == "role_bound":
                roles = {source.role for source in cited}
                if (
                    claim.kind in {"execution_outcome", "authorization_outcome"}
                    or (claim.kind == "policy" and "documentation" not in roles)
                    or (claim.kind == "case_fact" and "request_context" not in roles)
                    or (claim.kind == "recommendation" and not claim.condition.strip())
                ):
                    blocked += 1
                    continue
            if self.mode == "extractive":
                for citation, source in zip(claim.citations, cited):
                    attribution = source.document.document_title
                    output.append(f'> {citation.quote}\n\nSource: {attribution}')
            else:
                conditional = f"If {claim.condition.strip()}: " if claim.condition.strip() else ""
                output.append(conditional + claim.text)
        output.extend(f"Missing fact: {fact}" for fact in answer.missing_facts if fact.strip())
        if blocked:
            logger.warning(
                "Decision contract blocked %d/%d claims (mode=%s)",
                blocked, len(answer.claims), self.mode,
            )
            output.append(
                f"Evidence check: {blocked} proposed claim(s) could not be "
                "substantiated by this evidence contract and were withheld. "
                "This answer does not establish execution or review approval."
            )
        return "\n\n".join(dict.fromkeys(output))


def validate_mode(mode: str) -> str:
    if mode not in MODES:
        raise ValueError(f"Unknown API_SPEC_DECISION_MODE: {mode!r}")
    return mode


async def prepare_decision_context(
    req: ChatRequest, mode: str, knowledge_service: KnowledgeService | None = None,
) -> DecisionContext | None:
    validate_mode(mode)
    if not mode or req.tenant_id != TenantID.API_SPEC_REVIEW_BOT:
        return None
    result = await (knowledge_service or KnowledgeService()).retrieve(
        KnowledgeRetrieveRequest(
            query=req.message.content,
            tenant_id=req.tenant_id,
            with_wiki_search=True,
        )
    )
    evidence: list[DecisionEvidence] = []
    seen: set[str] = set()
    for reference in result.knowledge_list or []:
        if not reference.content.strip():
            continue
        document = DocumentContext.from_reference(reference)
        identity = hashlib.sha256(
            json.dumps(document.model_dump(mode="json"), sort_keys=True).encode()
        ).hexdigest()
        if identity in seen:
            continue
        seen.add(identity)
        evidence.append(DecisionEvidence(
            source_id=identity, role="documentation", document=document,
        ))
    evidence.append(DecisionEvidence(
        source_id="request_context",
        role="request_context",
        document=DocumentContext(
            document_title="User's account (not independently verified)",
            document_source="request_context",
            document_content=req.message.content,
        ),
    ))
    logger.info(
        "Decision evidence acquired: mode=%s, documents=%d, digest=%s",
        mode, len(evidence) - 1,
        hashlib.sha256("".join(item.source_id for item in evidence).encode()).hexdigest(),
    )
    return DecisionContext(mode=mode, evidence=tuple(evidence))
