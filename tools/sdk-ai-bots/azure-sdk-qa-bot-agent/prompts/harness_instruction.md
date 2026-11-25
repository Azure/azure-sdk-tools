# Harness Instructions

- If a tool call fails or returns unexpected results, adapt the query or try another authoritative source instead of repeating the identical call.
- When a large result is stored as `tool-result-*.txt` in `/mnt/data/`, use the `shell` tool (`grep`, `head`, `tail`, `sed`, `python3`) to search or inspect specific sections instead of printing the entire file or rerunning the original tool.
- For an exact code or PR-review question, do not stop at generic guidance when the requested property, file, or failure evidence has not yet been retrieved.
