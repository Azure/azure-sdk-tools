"""Azure SDK QA Bot — Wiki + Graph Index.

Builds LLM-derived knowledge layers over the corpus and writes them into the
*same* Azure AI Search index the KB path already uses, so a single
``search_knowledge_base`` call retrieves ordinary chunks and generated pages
together through one hybrid (vector + keyword) RRF + rerank pass.

There are **two independent creation pipelines** (mirroring WeKnora's separate
``WikiEnabled`` / ``GraphEnabled`` toggles):

* **wiki** (:mod:`wiki`) — per-document LLM synthesis into one dense knowledge
  page each (``page_type="wiki"``). ``context_id`` inherits the source
  document's, so existing tenant scoping applies unchanged.
* **graph** (:mod:`graph`) — LLM entity extraction + LLM relationship extraction
  (with strength), PMI + strength weighting, degree, and 1-hop/2-hop edges,
  producing ``page_type="entity"`` (``context_id="wiki_entity"``) and
  ``page_type="relationship"`` (``context_id="wiki_relationship"``) pages.

Each page keeps ``chunk_refs`` back to its source documents and ``related_slugs``
to neighbouring graph nodes. Pages are embedded with the index's own embedding
model so they share the KB vector space.
"""

from __future__ import annotations

__version__ = "2.0.0"

