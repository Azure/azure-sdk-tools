"""
Reasoning evaluator for the customization classifier skill.

For PHASE_A classifications, validates that reasoning references specific
TypeSpec decorators from the customizing-client-tsp.md reference document.

Decorators are dynamically extracted from the reference document to ensure
the validation stays in sync with the source of truth.
"""

import re
from functools import lru_cache
from pathlib import Path


def find_reference_document() -> Path:
    """Find the customizing-client-tsp.md reference document."""
    current_dir = Path(__file__).parent
    repo_root = current_dir.parents[3]  # .github/skills/customization-classifier/tests -> repo root
    
    # Search locations in priority order
    search_paths = [
        # azure-rest-api-specs (primary source)
        repo_root.parent / "azure-rest-api-specs" / "eng" / "common" / "knowledge" / "customizing-client-tsp.md",
        # azure-sdk-tools (local copy)
        repo_root / "eng" / "common" / "knowledge" / "customizing-client-tsp.md",
    ]
    
    for path in search_paths:
        if path.exists():
            return path
    
    raise FileNotFoundError(
        f"Could not find customizing-client-tsp.md. Searched:\n" +
        "\n".join(f"  - {p}" for p in search_paths)
    )


@lru_cache(maxsize=1)
def extract_decorators_from_reference() -> list[str]:
    """
    Dynamically extract decorator names from the reference document.
    
    Parses headings like '### @access' and '### @clientName' to build
    the list of known decorators.
    
    Uses LRU cache to avoid re-parsing the file on every test.
    """
    doc_path = find_reference_document()
    content = doc_path.read_text(encoding="utf-8")
    
    # Pattern: ### @decoratorName (possibly with additional text like "(C# only)")
    pattern = r'^### (@\w+)'
    matches = re.findall(pattern, content, re.MULTILINE)
    
    # Deduplicate while preserving order
    seen = set()
    decorators = []
    for match in matches:
        if match not in seen:
            seen.add(match)
            decorators.append(match)
    
    return decorators


def get_known_decorators() -> list[str]:
    """
    Get the list of known decorators from the reference document.
    
    Falls back to a minimal hardcoded list if the document cannot be found,
    to allow tests to run in isolation.
    """
    try:
        return extract_decorators_from_reference()
    except FileNotFoundError:
        # Fallback for running tests in isolation
        return [
            "@access",
            "@client", 
            "@clientName",
            "@operationGroup",
            "@clientLocation",
        ]


def check_decorator_reference(text: str) -> dict:
    """
    Check if the text references specific decorators from the reference doc.
    
    Returns a dict with:
      - referenced_decorators: list of decorators found
      - has_decorator_reference: bool
    """
    text_lower = text.lower()
    known_decorators = get_known_decorators()
    found = []
    
    for decorator in known_decorators:
        # Check for decorator name (case-insensitive, with or without @)
        decorator_name = decorator.lstrip("@").lower()
        if decorator_name in text_lower or decorator.lower() in text_lower:
            found.append(decorator)
    
    return {
        "referenced_decorators": found,
        "has_decorator_reference": len(found) > 0,
    }


def evaluate_phase_a_reasoning(reasoning: str, next_action: str = None) -> dict:
    """
    Evaluate PHASE_A reasoning for grounding in reference document.
    
    Args:
        reasoning: The reasoning text from the classifier
        next_action: The next action text (optional, also checked for decorators)
        
    Returns:
        dict with evaluation results:
          - grounded: bool - whether reasoning is grounded
          - decorator_check: dict - decorator reference analysis
          - details: str - explanation
    """
    # Combine reasoning and next_action since decorators may appear in either
    combined_text = reasoning
    if next_action:
        combined_text = f"{reasoning} {next_action}"
    
    decorator_check = check_decorator_reference(combined_text)
    
    if decorator_check["has_decorator_reference"]:
        return {
            "grounded": True,
            "decorator_check": decorator_check,
            "details": f"Decorator check passed: {', '.join(decorator_check['referenced_decorators'])}",
        }
    else:
        known = get_known_decorators()
        examples = ', '.join(known[:3]) if len(known) >= 3 else ', '.join(known)
        return {
            "grounded": False,
            "decorator_check": decorator_check,
            "details": (
                "PHASE_A reasoning should reference specific TypeSpec decorators "
                f"(e.g., {examples}) from the customization reference."
            ),
        }


def evaluate_reasoning(classification: str, reasoning: str, query: str, next_action: str = None) -> dict:
    """
    Evaluate reasoning based on classification type.
    
    Args:
        classification: PHASE_A, SUCCESS, or FAILURE
        reasoning: The reasoning text
        query: The original query (unused, kept for API compatibility)
        next_action: The next action text (optional)
        
    Returns:
        dict with:
          - valid: bool - whether reasoning is acceptable
          - evaluation_type: str - type of evaluation performed
          - details: str - explanation
          - (additional fields depending on classification)
    """
    if classification == "PHASE_A":
        phase_a_result = evaluate_phase_a_reasoning(reasoning, next_action)
        return {
            "valid": phase_a_result["grounded"],
            "evaluation_type": "decorator_check",
            **phase_a_result,
        }
    
    elif classification == "SUCCESS":
        # SUCCESS just needs to acknowledge completion
        has_completion_indicator = any(
            term in reasoning.lower() 
            for term in ["success", "complete", "passed", "build succeeded", "finished"]
        )
        return {
            "valid": True,  # Don't fail on SUCCESS reasoning
            "evaluation_type": "completion_check",
            "has_completion_indicator": has_completion_indicator,
            "details": "SUCCESS reasoning accepted." if has_completion_indicator else "SUCCESS reasoning could be clearer about completion.",
        }
    
    elif classification == "FAILURE":
        # FAILURE should explain why it can't proceed
        has_failure_explanation = any(
            term in reasoning.lower()
            for term in ["cannot", "unable", "not possible", "stall", "exceed", "iteration", "scope", "complex", "limit"]
        )
        return {
            "valid": True,  # Don't fail on FAILURE reasoning
            "evaluation_type": "failure_explanation_check",
            "has_failure_explanation": has_failure_explanation,
            "details": "FAILURE reasoning accepted." if has_failure_explanation else "FAILURE reasoning could better explain the issue.",
        }
    
    else:
        return {
            "valid": False,
            "evaluation_type": "unknown",
            "details": f"Unknown classification: {classification}",
        }
