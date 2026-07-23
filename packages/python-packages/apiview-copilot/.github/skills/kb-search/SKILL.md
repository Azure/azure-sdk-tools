---
name: kb-search
description: "Query the APIView Copilot knowledge base for guidelines, examples, and memories. Use for: search KB, search knowledge base, find guideline, lookup guideline, search guidelines, search examples, search memories, KB query, knowledge base search, find rule, what does the KB say about."
argument-hint: "Language + query text (e.g. 'Python async') or knowledge base IDs"
---

# Knowledge Base Search

## When to Use
- Looking up Azure SDK design guidelines for a specific language
- Finding examples (good or bad) related to an API pattern
- Retrieving specific knowledge base items by ID
- Understanding what RAG context the AI reviewer sees for a given query
- Investigating what guidance exists for a topic (e.g. naming, async, pagination)

## How It Works

The knowledge base contains three entity types linked in a graph:
- **Guidelines** — Azure SDK design rules (e.g. "Methods returning collections should use paging")
- **Examples** — Good/bad code snippets linked to guidelines
- **Memories** — Lessons learned from reviewer feedback, linked to guidelines and examples

A search query hits the Azure AI Search index (semantic + vector search), then resolves all linked entities from Cosmos DB via breadth-first traversal. The result is the same RAG context the AI reviewer would see.

## Defaults

Unless the user says otherwise, always apply these defaults:
- **Language**: Required for all non-`--ids` queries (both `--text` and `--path`). Use the language the user is asking about.
- **Output**: Use `--markdown` for text/path queries (easier for the agent to read and summarize). `--markdown` is not allowed with `--ids`.
- **Query mode**: Use `--text` for natural language queries. Use `--path` to search from a file. Use `--ids` only when the user provides specific knowledge base IDs.

## Three Query Modes

### 1. Text Search (`--text`)

Requires `--language`. Searches the index semantically and returns linked guidelines, examples, and memories.

```bash
python cli.py kb search -l <language> --text "<query>" --markdown
```

### 2. File Search (`--path`)

Requires `--language`. Reads query text from a file and searches the index.

```bash
python cli.py kb search -l <language> --path <file> --markdown
```

### 3. ID Lookup (`--ids`)

Retrieves specific items by ID. No other flags allowed (no `--language`, `--text`, `--markdown`, `--path`).

```bash
python cli.py kb search --ids <id1> <id2> ...
```

## Running the Command

Run directly in a **foreground terminal** with a **60-second timeout** (`timeout: 60000`). By default, let the terminal capture the output so the agent can read it directly. If the output appears truncated (terminal truncates at ~60 KB), re-run with redirection to a file and read it back:

```powershell
python cli.py kb search -l <language> --text "<query>" --markdown | Out-File -Encoding UTF8 scratch/kb_output.md
```

Do **not** use `>` — it produces UTF-16 in PowerShell 5.1. Always use `| Out-File -Encoding UTF8`.

Then use `read_file` on `scratch/kb_output.md` to get the full results.

**Text search**:
```bash
python cli.py kb search -l <language> --text "<query>" --markdown
```

**File search**:
```bash
python cli.py kb search -l <language> --path <file> --markdown
```

**ID lookup**:
```bash
python cli.py kb search --ids <id1> <id2>
```

After the command completes, read the terminal output and summarize the findings for the user.

### Examples

```bash
# Search Python guidelines about async
python cli.py kb search -l python --text "async client methods" --markdown

# Search TypeScript guidelines about naming
python cli.py kb search -l typescript --text "method naming conventions" --markdown

# Search Java guidelines about pagination
python cli.py kb search -l java --text "pagination list operations" --markdown

# Retrieve specific items by ID
python cli.py kb search --ids guideline-abc123 example-def456

# JSON output instead of markdown (for structured processing)
python cli.py kb search -l dotnet --text "dispose pattern"
```

## Available Flags

| Flag | Type | Default | Description |
|------|------|---------|-------------|
| `--text` | string | — | Natural language search query (mutually exclusive with `--path`) |
| `--path` | string | — | Path to a file containing query text or code (mutually exclusive with `--text`) |
| `--ids` | list | — | One or more knowledge base item IDs to retrieve directly (no other flags allowed) |
| `--language` / `-l` | string | — | Language to search (required for all non-`--ids` queries) |
| `--markdown` | flag | off | Render output as markdown instead of JSON (not allowed with `--ids`) |

## Understanding the Output

### Markdown Format (`--markdown`)

Each result is rendered as a section with:
- **Metadata block**: `{kind}_id` (e.g. `guideline_id` or `memory_id`), score (if available), and exception status (if true)
- **Title**: The guideline or memory title
- **Content**: The full guideline or memory text
- **Examples**: Good and bad code examples in fenced code blocks

This is the same format the AI reviewer sees during RAG-based reviews.

### JSON Format (default)

Returns the full `Context` object with nested guidelines, examples, and memories including scores and all linked entities. Useful for programmatic inspection.

### ID Lookup Format

Returns `SearchItem` objects as a JSON array with a curated projection of index fields: `id`, `kind`, `title`, `content`, `language`, `service`, `is_exception`, `example_type`, and search scores.

## Gotchas

- **`--ids` is exclusive**: When using `--ids`, do not pass `--language`, `--text`, `--markdown`, or `--path`. The command will error.
- **`--language` is required for all non-`--ids` queries**: Both `--text` and `--path` require `--language` to filter the index.
- **Exactly one of `--text` or `--path`**: For non-ID queries, provide one but not both.
- **Use `python cli.py` not `avc`/`avc.bat`**: The wrapper scripts (`avc` on Linux/macOS, `avc.bat` on Windows) invoke the system `python`/`python3`, which may not be the correct virtual environment. Run `python cli.py` directly to ensure the right interpreter is used.
- **Do NOT use `2>&1`**: This merges stderr log messages into the output, corrupting it.
- **Prefer `--markdown` for text/path queries**: The markdown output is more readable and is what the AI reviewer actually sees. Use JSON only when you need structured data.
- **Scores are normalized**: Search result scores use Z-score normalization (mean=50). Higher is more relevant.
