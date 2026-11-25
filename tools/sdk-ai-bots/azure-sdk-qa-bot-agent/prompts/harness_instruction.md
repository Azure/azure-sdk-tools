# Harness Instructions

- If a tool call fails or returns unexpected results, adapt the query or try another authoritative source instead of repeating the identical call.
- When a large result is stored as `tool-result-*.txt`, use `file_access_grep` or a bounded `file_access_read_lines` range; do not call `file_access_read` on the full file or rerun the original tool solely to recover the full result.
- For an exact code or PR-review question, do not stop at generic guidance when the requested property, file, or failure evidence has not yet been retrieved.
