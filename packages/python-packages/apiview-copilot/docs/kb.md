# Knowledge Base (KB)

The AVC knowledge base is the foundation of the AI reviewer's accuracy. It stores the guidelines, examples, and operational memories that are retrieved via RAG (Retrieval-Augmented Generation) and injected into LLM prompts at review time.

## KB Entity Types

The knowledge base has three entity types, each stored in a dedicated Cosmos DB container and indexed in Azure AI Search.

### Guidelines (`guidelines` container)

A **Guideline** represents a single named rule or best practice from the Azure SDK design guidelines. Guidelines are the primary source of truth for what constitutes a good or bad API.

| Field | Description |
|-------|-------------|
| `id` | Unique identifier, typically matching the guideline anchor in the published docs (e.g., `python_design.general-namespaces`) |
| `title` | Short descriptive title |
| `content` | Full text of the guideline |
| `language` | Language the guideline applies to (e.g., `python`), or empty for cross-language guidelines |
| `tags` | Classification tags (e.g., `documentation`, `vague`) |
| `related_guidelines` | IDs of related guidelines |
| `related_examples` | IDs of related examples |
| `related_memories` | IDs of related memories |

> **Note on `id` format:** Guideline IDs containing `.` are stored in Cosmos DB with `|` as a separator (e.g., `python_design|general-namespaces`) because `.` is reserved in Cosmos DB partition paths. The application transparently converts between formats.

### Examples (`examples` container)

An **Example** is a code snippet illustrating either a correct (good) or incorrect (bad) API pattern. Examples are linked to guidelines and/or memories to provide concrete context during reviews.

| Field | Description |
|-------|-------------|
| `id` | Unique identifier |
| `title` | Short descriptive title |
| `content` | The code snippet |
| `language` | Language the example applies to |
| `service` | If example is service-specific (e.g., `azure-storage`) |
| `example_type` | `good` or `bad` |
| `is_exception` | If `true`, this example provides an exception to a guideline rather than amplifying it |
| `guideline_ids` | Guidelines to which this example applies |
| `memory_ids` | Memories to which this example applies |

### Memories (`memories` container)

A **Memory** is a learned observation from past API reviews. Memories are created automatically via two workflows:

1. **@mention handling** — When a reviewer @-mentions the bot with feedback (e.g., "this is correct"), a memory is created to prevent repeating the same comment.
2. **Thread resolution** — When a conversation thread is marked resolved with the bot's comment still open, a memory is created to encode the reviewer's decision.

| Field | Description |
|-------|-------------|
| `id` | Unique identifier |
| `title` | Short descriptive title |
| `content` | The learned observation or correction |
| `language` | Language the memory applies to |
| `service` | If the memory is service-specific |
| `source` | Origin of the memory: `mention_agent` or `thread_resolution` |
| `source_comment_id` | ID of the APIView comment that triggered the memory, for auditing |
| `is_exception` | If `true`, this memory provides an exception to a guideline |
| `related_guidelines` | Related guideline IDs |
| `related_examples` | Related example IDs |
| `related_memories` | Related memory IDs |

## Search Index

All three entity types are indexed together in a single **Azure AI Search** index. The index supports:

- **Full-text search** — keyword-based matching over `title` and `content` fields
- **Semantic (vector) search** — embedding-based similarity search for RAG context retrieval
- **Filtering** — by `language`, `kind` (entity type), `is_exception`, and `tags`

Each Cosmos DB container has a corresponding **Search Indexer** (e.g., `guidelines-indexer`, `examples-indexer`, `memories-indexer`) that syncs data from Cosmos DB into the search index. Indexers are triggered automatically after create/update/delete operations.

## How the KB Is Used at Review Time

During a review (`ApiViewReview.run()`), the KB is queried in two ways:

### 1. Guideline Context (full list)

All guidelines for the target language are pre-fetched at the start of each review and used as context for the **Guideline Review** prompt. This ensures the LLM has the full set of applicable rules before it sees the API text.

```
SearchManager.language_guidelines → Context → guidelines_review.prompty
```

### 2. Semantic Context (per-section)

For each API section, a semantic similarity search is performed against the unified index (guidelines + examples + memories) using the section text as the query. The top results form the context for the **Context Review** prompt.

```
section text → SearchManager.search_all(query) → Context → context_review.prompty
```

## Linking KB Items

KB items can be explicitly linked together to help the AI retrieve related items together. Linking is bidirectional: when you link a guideline to a memory, the memory's ID is added to the guideline's `related_memories` list, and vice versa.

```bash
avc db link -g <GUIDELINE_ID> -m <MEMORY_ID>
avc db link -g <GUIDELINE_ID> -e <EXAMPLE_ID>
avc db link -m <MEMORY_ID> -e <EXAMPLE_ID>
```

Use `--reindex` to trigger a full search index refresh after linking.

To remove a link:
```bash
avc db unlink -g <GUIDELINE_ID> -m <MEMORY_ID>
```

## Soft Deletion

All KB items support **soft deletion**: instead of removing the document from Cosmos DB, a `isDeleted: true` flag is set. Soft-deleted items are excluded from search results. This preserves audit history and simplifies rollback.

## Inspecting the KB Locally

To search the knowledge base and see what the LLM would receive as context:

```bash
# Search by text query for a specific language
avc kb search --text "error handling" -l python

# Output as Markdown (what the LLM sees)
avc kb search --text "naming conventions" -l dotnet --markdown > context.md

# Get all guidelines for a language
avc kb all-guidelines -l python

# Search by known item ID(s)
avc kb search --ids python_design.general-namespaces python_design.general-client
```

## Reindexing

If KB data is updated directly in Cosmos DB (outside of the CLI), or if the search index gets out of sync, trigger a reindex:

```bash
avc kb reindex
```

This runs all three indexers (`guidelines-indexer`, `examples-indexer`, `memories-indexer`). If an indexer is already running, the command skips it.
