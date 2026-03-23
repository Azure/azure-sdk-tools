"""Reusable LLM prompt execution utilities.

Provides :class:`PromptTemplate` for loading and rendering prompt/schema
pairs from disk, and :func:`execute_prompt` as the single entry-point for
calling the LLM and returning a parsed dict.

Typical usage
-------------
::

    from utils.llm import PromptTemplate, execute_prompt

    _ROUTING = PromptTemplate(
        prompt_file="tenant_routing.md",
        schema_file="tenant_routing_result_schema.json",
    )

    result = await execute_prompt(
        _ROUTING,
        variables={"original_tenant": "general_qa_bot"},
        user_message=summary,
    )
    tenant = result["route_tenant"]
"""

from __future__ import annotations

import json
import logging
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from utils.azure_ai_foundry import get_openai_client

logger = logging.getLogger(__name__)

# All prompt / schema files live under this directory.
_PROMPTS_DIR = Path(__file__).resolve().parent.parent / "prompts"


# ---------------------------------------------------------------------------
# Prompt template descriptor
# ---------------------------------------------------------------------------

@dataclass(frozen=True)
class PromptTemplate:
    """Describes a prompt + optional JSON schema pair on disk.

    Paths are relative to the project's ``prompts/`` directory.

    Attributes:
        prompt_file:  Relative path to the prompt markdown file
                      (e.g. ``"tenant_routing.md"``).
        schema_file:  Relative path to the JSON schema file used for
                      structured outputs.  If empty, the LLM is called
                      with ``response_format={"type": "json_object"}``.
    """

    prompt_file: str
    schema_file: str = ""

    # -- derived paths (computed once) --

    @property
    def prompt_path(self) -> Path:
        return _PROMPTS_DIR / self.prompt_file

    @property
    def schema_path(self) -> Path | None:
        return (_PROMPTS_DIR / self.schema_file) if self.schema_file else None

    # -- loaders --

    def load_prompt(self, variables: dict[str, str] | None = None) -> str:
        """Load the prompt file and substitute ``{{key}}`` placeholders."""
        path = self.prompt_path
        if not path.exists():
            raise FileNotFoundError(f"Prompt file not found: {path}")
        text = path.read_text(encoding="utf-8")
        for key, value in (variables or {}).items():
            text = text.replace("{{" + key + "}}", value)
        return text

    def load_schema(self) -> dict[str, Any] | None:
        """Load the JSON schema, or ``None`` if no schema file was specified."""
        path = self.schema_path
        if path is None:
            return None
        if not path.exists():
            raise FileNotFoundError(f"Schema file not found: {path}")
        return json.loads(path.read_text(encoding="utf-8"))


# ---------------------------------------------------------------------------
# Prompt execution
# ---------------------------------------------------------------------------

async def execute_prompt(
    template: PromptTemplate,
    *,
    variables: dict[str, str] | None = None,
    user_message: str,
    model: str,
    **kwargs: Any,
) -> dict[str, Any]:
    """Execute a prompt template against the LLM and return the parsed JSON.

    Parameters
    ----------
    template:
        The :class:`PromptTemplate` describing what to send.
    variables:
        Substitution variables for ``{{key}}`` placeholders in the prompt.
    user_message:
        The user-role content (e.g. the question or summary).
    model:
        The model deployment name to use for the LLM call.
    **kwargs:
        Extra keyword arguments forwarded to
        ``openai_client.chat.completions.create()`` (e.g. ``reasoning_effort``).

    Returns
    -------
    dict
        The parsed JSON response from the LLM.

    Raises
    ------
    FileNotFoundError
        If the prompt or schema file is missing.
    LLMError
        If the LLM call fails, returns no choices, or produces
        unparseable output.
    """
    system_prompt = template.load_prompt(variables)
    schema = template.load_schema()

    response_format: dict[str, Any] = schema if schema else {"type": "json_object"}

    openai_client = get_openai_client()
    try:
        response = await openai_client.chat.completions.create(
            model=model,
            messages=[
                {"role": "system", "content": system_prompt},
                {"role": "user", "content": user_message},
            ],
            response_format=response_format,
            **kwargs,
        )
    except Exception as exc:
        raise LLMError(f"LLM call failed: {exc}") from exc

    if not response.choices:
        raise LLMError("LLM returned no choices")

    content = response.choices[0].message.content or ""
    try:
        return json.loads(content)
    except json.JSONDecodeError as exc:
        raise LLMError(
            f"Failed to parse LLM JSON response: {content[:200]}"
        ) from exc


# ---------------------------------------------------------------------------
# Exceptions
# ---------------------------------------------------------------------------

class LLMError(Exception):
    """Raised when an LLM prompt execution fails."""
