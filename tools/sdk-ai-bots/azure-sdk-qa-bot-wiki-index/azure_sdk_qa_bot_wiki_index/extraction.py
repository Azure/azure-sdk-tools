"""Entity + concept discovery for cross-document wiki pages.

Grounded, LLM-free discovery of what to write cross-document pages about:

* **entities** — decorator / symbol tokens (``@added``, ``@versioned`` …) that
  recur across the corpus above a frequency threshold. For each, gather the
  section excerpts that mention it (the raw material the entity page synthesises).
* **concepts** — a curated seed list of core Azure SDK / TypeSpec topics; a
  concept is materialised only when enough documents discuss it. Excerpts are the
  matching sections.

Discovery is deterministic (regex + keyword match); the synthesis of the page
text from the gathered excerpts is done by the LLM in :mod:`synthesis`.
"""

from __future__ import annotations

import logging
import re
from collections import defaultdict

logger = logging.getLogger(__name__)

# ``@decorator`` and ``@@augmentDecorator`` tokens.
_DECORATOR_RE = re.compile(r"@@?[A-Za-z][A-Za-z0-9_]*")

# Core cross-document topics worth a concept page (seed list; a concept is only
# emitted when >= min_docs documents mention it).
_CONCEPT_SEEDS: dict[str, list[str]] = {
    "API versioning": ["versioning", "api version", "@added", "@removed", "@versioned"],
    "Long-running operations": ["long-running", "long running", "lro", "status monitor", "polling"],
    "Pagination": ["pagination", "paging", "@pageable", "nextlink", "@list"],
    "ARM resources": ["trackedresource", "armresource", "resource manager", "arm resource"],
    "Error handling": ["@error", "error response", "errorresponse", "problem details"],
    "Authentication": ["@useauth", "authentication", "oauth2", "apikey", "bearer"],
    "Client customization": ["client.tsp", "@clientname", "@access", "@usedependency"],
    "Naming and casing": ["@encodedname", "naming", "casing", "camelcase"],
    "Discriminated unions": ["@discriminator", "discriminated union", "polymorphism"],
    "Spread and models": ["spread", "...", "model is", "template"],
}

_MAX_EXCERPTS = 12
_MAX_EXCERPT_CHARS = 900


def _sections(text: str) -> list[str]:
    """Split a document into header-delimited sections (crude, for excerpting)."""
    parts = re.split(r"(?m)^#{1,6}\s+", text)
    return [p.strip() for p in parts if p.strip()]


def discover_entities(
    corpus: list[tuple[str, str]],
    *,
    min_docs: int = 4,
    max_entities: int = 60,
) -> dict[str, list[str]]:
    """Return ``{decorator: [excerpts]}`` for decorators recurring across docs."""
    doc_count: dict[str, set[str]] = defaultdict(set)
    excerpts: dict[str, list[str]] = defaultdict(list)

    for source_path, text in corpus:
        secs = _sections(text)
        for tok in set(_DECORATOR_RE.findall(text)):
            key = tok.lstrip("@")
            doc_count[key].add(source_path)
        # attach the most relevant section per decorator (first mention)
        for sec in secs:
            for tok in set(_DECORATOR_RE.findall(sec)):
                key = tok.lstrip("@")
                if len(excerpts[key]) < _MAX_EXCERPTS:
                    excerpts[key].append(sec[:_MAX_EXCERPT_CHARS])

    ranked = sorted(
        ((k, len(docs)) for k, docs in doc_count.items() if len(docs) >= min_docs),
        key=lambda kv: kv[1],
        reverse=True,
    )[:max_entities]
    result = {f"@{k}": excerpts[k] for k, _ in ranked if excerpts[k]}
    logger.info("discover_entities: %d entities (min_docs=%d)", len(result), min_docs)
    return result


def discover_concepts(
    corpus: list[tuple[str, str]],
    *,
    min_docs: int = 3,
) -> dict[str, list[str]]:
    """Return ``{concept: [excerpts]}`` for seed topics with enough coverage."""
    result: dict[str, list[str]] = {}
    for concept, keywords in _CONCEPT_SEEDS.items():
        matching_docs = 0
        excerpts: list[str] = []
        for _source_path, text in corpus:
            low = text.lower()
            if not any(kw in low for kw in keywords):
                continue
            matching_docs += 1
            for sec in _sections(text):
                sl = sec.lower()
                if any(kw in sl for kw in keywords) and len(excerpts) < _MAX_EXCERPTS:
                    excerpts.append(sec[:_MAX_EXCERPT_CHARS])
        if matching_docs >= min_docs and excerpts:
            result[concept] = excerpts
    logger.info("discover_concepts: %d concepts", len(result))
    return result
