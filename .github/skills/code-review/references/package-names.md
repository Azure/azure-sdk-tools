# Canonical Release CSV Naming Policy

Read `.github/instructions/release-csv-names.instructions.md` in the
Azure/azure-sdk repository being reviewed, using a read-only file tool. It is
the canonical deployed policy, including scope, unknown-field handling,
evidence requirements, and advisory feedback. This adapter intentionally does
not duplicate its rules.

For excerpt evaluations, the workspace contains a pinned Azure/azure-sdk
checkout with that exact instruction file. Read it before assessing the CSV
diff. No shell, network lookup, or second policy snapshot is needed.

If the canonical file is unavailable, report that limitation rather than
inventing review rules or falling back to a stale mirror.
