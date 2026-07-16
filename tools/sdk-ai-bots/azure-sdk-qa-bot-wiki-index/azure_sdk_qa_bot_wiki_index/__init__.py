"""Azure SDK QA Bot — Wiki Index.

Builds an LLM-synthesised **wiki layer** over the knowledge corpus and writes it
into the *same* Azure AI Search index the KB path already uses, so a single
``search_knowledge_base`` call retrieves ordinary document chunks and wiki pages
together through one hybrid (vector + keyword) RRF + rerank pass.

Three page types are generated (mirroring the reference wiki-mode design):

* **summary** — one dense, declarative knowledge card per source document
  (facts, exact decorator/API names, rules, defaults, gotchas). ``context_id``
  inherits the source document's, so existing tenant scoping applies unchanged.
* **entity** — one page per recurring decorator / API / symbol, aggregating what
  the whole corpus says about it (cross-document). ``context_id="wiki_entity"``.
* **concept** — one page per topic / methodology, cross-document.
  ``context_id="wiki_concept"``.

Each page keeps ``chunk_refs`` back to its source documents (for deep-read) and
``related_slugs`` to other pages (page-to-page links). Pages are embedded with
the index's own embedding model so they share the KB vector space.
"""

from __future__ import annotations

__version__ = "1.0.0"
