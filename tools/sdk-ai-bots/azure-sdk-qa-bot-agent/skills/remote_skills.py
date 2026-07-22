"""Remote Agent Skills for the Azure SDK QA Bot Agent.

The Microsoft Agent Framework models skills through the abstract
:class:`agent_framework.SkillsSource`. A source is free to discover skills
from *any* origin — filesystem, memory, or the network — which is exactly the
extension point needed to add a **remote** skill hosted in another GitHub
repository.

This module implements :class:`RemoteAgentSkillsSource`, which fetches an
`Agent Skill <https://agentskills.io/specification>`_ (a ``SKILL.md`` file plus
``references/`` files) from raw GitHub URLs and turns it into an
:class:`agent_framework.InlineSkill`:

  - The ``SKILL.md`` body becomes the skill instructions (advertised on
    ``load_skill``).
  - Each reference file listed in ``SKILL.md`` becomes a
    :class:`agent_framework.InlineSkillResource` that is fetched lazily the
    first time the agent calls ``read_skill_resource``.

It is used to integrate the shared **azure-api-review** skill from
``azure-rest-api-specs`` so the bot can answer Azure REST API shape/design
review questions (naming, property mutability, provisioning state, enums,
versioning, breaking changes, ARM RPC compliance).
"""

from __future__ import annotations

import logging
import re

import httpx
import yaml
from agent_framework import (
    InlineSkill,
    InlineSkillResource,
    Skill,
    SkillFrontmatter,
    SkillsSource,
)

logger = logging.getLogger(__name__)

# ---------------------------------------------------------------------------
# azure-api-review skill location (azure-rest-api-specs)
# ---------------------------------------------------------------------------
# Raw base URL of the skill directory. The trailing slash is required so that
# ``SKILL.md`` and ``references/*.md`` resolve relative to it.
AZURE_API_REVIEW_SKILL_URL = (
    "https://raw.githubusercontent.com/Azure/azure-rest-api-specs/"
    "main/.github/skills/azure-api-review/"
)

_DEFAULT_TIMEOUT = 15.0

# Matches an ``[label](references/foo.md)`` reference-file link, optionally
# followed by a markdown table cell describing the rule area.
_REFERENCE_LINK_RE = re.compile(r"\[[^\]]+\]\((references/[^)]+\.md)\)")
_REFERENCE_ROW_RE = re.compile(
    r"^\|\s*\[[^\]]+\]\((references/[^)]+\.md)\)\s*\|\s*([^|]+?)\s*\|",
    re.MULTILINE,
)


def _split_frontmatter(text: str) -> tuple[dict[str, object], str]:
    """Split a ``SKILL.md`` document into ``(frontmatter, body)``.

    Returns an empty frontmatter dict and the original text unchanged when no
    ``---`` delimited YAML frontmatter block is present.
    """
    if not text.startswith("---"):
        return {}, text
    end = text.find("\n---", 3)
    if end == -1:
        return {}, text
    fm_block = text[3:end].strip()
    body = text[end + 4 :].lstrip("\n")
    try:
        parsed = yaml.safe_load(fm_block)
    except yaml.YAMLError:
        return {}, text
    if not isinstance(parsed, dict):
        return {}, text
    return parsed, body


def _parse_reference_descriptions(body: str) -> dict[str, str]:
    """Map each ``references/*.md`` path to its rule-area description.

    Descriptions are taken from the reference table's second column when
    available; paths not found in a table row are omitted from the map.
    """
    descriptions: dict[str, str] = {}
    for path, rule_area in _REFERENCE_ROW_RE.findall(body):
        descriptions.setdefault(path, rule_area.strip())
    return descriptions


class RemoteAgentSkillsSource(SkillsSource):
    """A :class:`~agent_framework.SkillsSource` backed by remote GitHub URLs.

    Fetches a single Agent Skill (``SKILL.md`` + ``references/``) from
    *raw_base_url* and exposes it as an :class:`~agent_framework.InlineSkill`.
    Network and parsing failures are logged and degrade gracefully to an empty
    skill list so the agent still starts.
    """

    def __init__(
        self,
        *,
        raw_base_url: str,
        timeout: float = _DEFAULT_TIMEOUT,
    ) -> None:
        """Initialize the source.

        Args:
            raw_base_url: Raw base URL of the skill directory (must end with
                ``/``) so that ``SKILL.md`` and ``references/*.md`` resolve
                relative to it.
            timeout: Per-request HTTP timeout in seconds.
        """
        self._base_url = raw_base_url if raw_base_url.endswith("/") else raw_base_url + "/"
        self._timeout = timeout

    async def _get_text(self, url: str) -> str:
        """Fetch *url* and return its text body, raising on HTTP errors."""
        async with httpx.AsyncClient(
            timeout=self._timeout, follow_redirects=True
        ) as client:
            resp = await client.get(url)
            resp.raise_for_status()
            return resp.text

    def _build_resource(self, path: str, description: str | None) -> InlineSkillResource:
        """Build a lazily-fetched, cached resource for reference file *path*."""
        url = self._base_url + path
        cache: dict[str, str] = {}

        async def _read() -> str:
            if "content" not in cache:
                try:
                    cache["content"] = await self._get_text(url)
                except Exception as exc:  # noqa: BLE001 - report to the agent
                    logger.warning("Failed to read skill resource %s: %s", url, exc)
                    return f"Error: unable to fetch resource '{path}' from {url}: {exc}"
            return cache["content"]

        return InlineSkillResource(
            name=path,
            description=description,
            function=_read,
        )

    async def get_skills(self) -> list[Skill]:
        """Fetch the remote skill and return it as a one-element list.

        Returns an empty list if the skill cannot be fetched or parsed.
        """
        try:
            skill_md = await self._get_text(self._base_url + "SKILL.md")
        except Exception as exc:  # noqa: BLE001 - startup must not crash
            logger.warning(
                "Skipping remote skill at %s: %s", self._base_url, exc
            )
            return []

        frontmatter, body = _split_frontmatter(skill_md)
        name = str(frontmatter.get("name", "")).strip()
        description = str(frontmatter.get("description", "")).strip()
        if not name or not description or not body.strip():
            logger.warning(
                "Skipping remote skill at %s: missing name/description/body",
                self._base_url,
            )
            return []

        ref_descriptions = _parse_reference_descriptions(body)
        seen: set[str] = set()
        resources: list[InlineSkillResource] = []
        for path in _REFERENCE_LINK_RE.findall(body):
            if path in seen:
                continue
            seen.add(path)
            resources.append(self._build_resource(path, ref_descriptions.get(path)))

        try:
            skill = InlineSkill(
                frontmatter=SkillFrontmatter(name=name, description=description),
                instructions=body,
                resources=resources,
            )
        except ValueError as exc:
            logger.warning(
                "Skipping remote skill %s: invalid skill metadata: %s", name, exc
            )
            return []

        logger.info(
            "Loaded remote skill: %s (%d reference resources)", name, len(resources)
        )
        return [skill]


async def load_remote_skills(
    *,
    enabled: bool = True,
    azure_api_review_url: str = AZURE_API_REVIEW_SKILL_URL,
) -> list[Skill]:
    """Fetch all remotely-hosted Agent Skills for the chat agent.

    Currently loads the shared **azure-api-review** skill from
    ``azure-rest-api-specs``. Always returns a list (possibly empty); failures
    are logged and never raised so the agent can start without the skill.

    Args:
        enabled: When ``False``, skip loading and return an empty list.
        azure_api_review_url: Raw base URL of the azure-api-review skill,
            overridable via configuration.
    """
    if not enabled:
        logger.info("Remote skills disabled; skipping azure-api-review.")
        return []
    source = RemoteAgentSkillsSource(raw_base_url=azure_api_review_url)
    return await source.get_skills()
