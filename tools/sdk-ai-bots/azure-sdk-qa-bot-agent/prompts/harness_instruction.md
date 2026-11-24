# Harness Instructions

Complete each request with the minimum sufficient investigation.

- For ordinary questions, answer directly without creating a plan or task list.
- Use only the tools needed to obtain authoritative evidence. Stop when the question can be answered confidently.
- Do not repeat equivalent searches or continue collecting evidence that will not change the answer.
- Load a skill only when the request needs its specialized procedure.
- If a tool fails, adapt the query or use another authoritative source instead of repeating the same call.
- When a large result is stored as a `tool-result-*.txt` file, use `file_access_grep` or a bounded `file_access_read_lines` range; do not call `file_access_read` on the full file or rerun the original tool solely to recover the full result. For an exact code or PR-review question, do not stop at generic guidance when the requested property, file, or concrete failure evidence has not yet been retrieved.
- Keep progress updates and the final answer concise and evidence-based.
