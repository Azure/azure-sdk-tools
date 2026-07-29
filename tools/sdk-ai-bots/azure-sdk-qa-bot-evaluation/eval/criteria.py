"""Builders for evaluation testing criteria (Foundry OpenAI-evals surface).

All evaluators are **builtin LLM** evaluators that score the bot answer (collected
from ``/completion`` and carried in the inline eval item). They read the answer via
``{{item.response}}`` and, for groundedness, the retrieved context via
``{{item.context}}``. Three families:

* **LLM-graded quality with threshold** (model + 1-5 scale):
  ``similarity``, ``response_completeness``.
* **LLM-graded quality (model only; query + response)**:
  ``relevance``, ``coherence``, ``fluency``.
* **LLM-graded groundedness** (``deployment_name`` + retrieved context):
  ``groundedness``.

The ``bot_evals`` weighted composite (similarity + response_completeness) is **not**
a criterion; it is computed locally in ``_evals_result`` from the per-criterion
scores (decision O5).
"""

from __future__ import annotations

from typing import Any

# LLM-graded evaluators that take a 1-5 ``threshold`` + ``model``.
LLM_THRESHOLD_EVALUATORS = ("similarity", "response_completeness")
# LLM-graded evaluators that take only ``model`` (query + response).
LLM_MODEL_EVALUATORS = ("relevance", "coherence", "fluency")

# All builtin evaluators selectable via ``--evaluators``.
BUILTIN_EVALUATORS = (
    *LLM_THRESHOLD_EVALUATORS,
    "groundedness",
    *LLM_MODEL_EVALUATORS,
)

RESPONSE_REF = "{{item.response}}"
CONTEXT_REF = "{{item.context}}"


def _criterion(**kwargs: Any) -> Any:
    from azure.ai.projects.models import TestingCriterionAzureAIEvaluator

    return TestingCriterionAzureAIEvaluator(type="azure_ai_evaluator", **kwargs)


# --- LLM-graded quality (model + 1-5 threshold) ---------------------------------

def similarity_criterion(model: str, threshold: int = 3) -> Any:
    return _criterion(
        name="similarity",
        evaluator_name="builtin.similarity",
        initialization_parameters={"model": model, "threshold": threshold},
        data_mapping={
            "query": "{{item.query}}",
            "response": RESPONSE_REF,
            "ground_truth": "{{item.ground_truth}}",
        },
    )


def response_completeness_criterion(model: str, threshold: int = 3) -> Any:
    return _criterion(
        name="response_completeness",
        evaluator_name="builtin.response_completeness",
        initialization_parameters={"model": model, "threshold": threshold},
        data_mapping={
            "response": RESPONSE_REF,
            "ground_truth": "{{item.ground_truth}}",
        },
    )


# --- LLM-graded quality (model only; query + response) --------------------------

def relevance_criterion(model: str) -> Any:
    return _criterion(
        name="relevance",
        evaluator_name="builtin.relevance",
        initialization_parameters={"model": model},
        data_mapping={"query": "{{item.query}}", "response": RESPONSE_REF},
    )


def coherence_criterion(model: str) -> Any:
    return _criterion(
        name="coherence",
        evaluator_name="builtin.coherence",
        initialization_parameters={"model": model},
        data_mapping={"query": "{{item.query}}", "response": RESPONSE_REF},
    )


def fluency_criterion(model: str) -> Any:
    return _criterion(
        name="fluency",
        evaluator_name="builtin.fluency",
        initialization_parameters={"model": model},
        data_mapping={"query": "{{item.query}}", "response": RESPONSE_REF},
    )


# --- LLM-graded groundedness (deployment_name + retrieved context) --------------

def groundedness_criterion(model: str) -> Any:
    # Groundedness is LLM-graded; we feed the bot's retrieved context so the judge
    # sees the evidence the bot grounded on.
    return _criterion(
        name="groundedness",
        evaluator_name="builtin.groundedness",
        initialization_parameters={"deployment_name": model},
        data_mapping={
            "query": "{{item.query}}",
            "response": RESPONSE_REF,
            "context": CONTEXT_REF,
        },
    )


_LLM_THRESHOLD_BUILDERS = {
    "similarity": similarity_criterion,
    "response_completeness": response_completeness_criterion,
}
_LLM_MODEL_BUILDERS = {
    "relevance": relevance_criterion,
    "coherence": coherence_criterion,
    "fluency": fluency_criterion,
}


def build_testing_criteria(
    evaluators: list[str],
    *,
    model: str,
    threshold: int = 3,
) -> list[Any]:
    """Build the ``testing_criteria`` list for the requested evaluator names.

    Unknown names are ignored. ``bot_evals`` expands to its builtin components
    (similarity + response_completeness) since the composite is computed locally.
    """
    requested: list[str] = []
    for name in evaluators:
        if name == "bot_evals":
            requested.extend(["similarity", "response_completeness"])
        else:
            requested.append(name)

    # De-dup while preserving order.
    seen: set[str] = set()
    ordered = [n for n in requested if not (n in seen or seen.add(n))]

    criteria: list[Any] = []
    for name in ordered:
        if name in _LLM_THRESHOLD_BUILDERS:
            criteria.append(_LLM_THRESHOLD_BUILDERS[name](model, threshold))
        elif name == "groundedness":
            criteria.append(groundedness_criterion(model))
        elif name in _LLM_MODEL_BUILDERS:
            criteria.append(_LLM_MODEL_BUILDERS[name](model))
    return criteria


__all__ = [
    "BUILTIN_EVALUATORS",
    "LLM_THRESHOLD_EVALUATORS",
    "LLM_MODEL_EVALUATORS",
    "build_testing_criteria",
    "similarity_criterion",
    "response_completeness_criterion",
    "groundedness_criterion",
    "relevance_criterion",
    "coherence_criterion",
    "fluency_criterion",
]
