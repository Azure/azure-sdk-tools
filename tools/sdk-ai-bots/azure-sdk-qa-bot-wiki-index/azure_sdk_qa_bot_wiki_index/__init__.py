"""Azure SDK QA Bot — Wiki Index.

Builds an LLM-derived **wiki layer** over the corpus and writes it into the KB's
Azure AI Search index, so a single ``search_knowledge_base`` call retrieves raw
chunks and wiki pages together through one hybrid (vector + keyword) RRF +
rerank pass.

Faithful to WeKnora's wiki layer (``wiki_ingest.go``): a **MapReduce** produces
four page types —

* **summary**  — one dense page per source document (``context_id`` inherits the
  source folder, so existing tenant scoping applies unchanged).
* **entity**   — cross-document, one per recurring symbol (map extracts, reduce
  aggregates + dedups + synthesises). ``context_id="wiki_entity"``.
* **concept**  — cross-document, one per topic. ``context_id="wiki_concept"``.
* **index**    — a navigation page.

Pages are **cross-linked** by shared source documents (WeKnora
``injectCrossLinks``). The separate PMI relationship-graph layer was removed.
"""

from __future__ import annotations

__version__ = "3.0.0"

