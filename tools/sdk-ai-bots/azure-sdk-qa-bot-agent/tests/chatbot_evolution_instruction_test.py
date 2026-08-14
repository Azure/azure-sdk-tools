"""Contract tests for Chatbot Evolution Agent verdict instructions."""

from pathlib import Path


def test_explicit_expert_problem_signals_cannot_be_overruled() -> None:
    instruction = (
        Path(__file__).resolve().parent.parent
        / "agents"
        / "chatbot_evolution_agent"
        / "instruction.md"
    ).read_text(encoding="utf-8")
    normalized = " ".join(instruction.split())

    assert "must not" in normalized
    assert "return `no_issue`" in normalized
    assert "documentation must be added, clarified, or improved" in normalized
    assert "do not use retrieved evidence to overrule" in normalized


def test_evaluator_treats_documentation_gap_as_incorrect() -> None:
    prompt = (
        Path(__file__).resolve().parent.parent
        / "prompts"
        / "conversation_evaluation.md"
    ).read_text(encoding="utf-8")

    assert "documentation-gap signal is still `incorrect`" in prompt
    assert "Do not independently overrule that signal" in prompt
