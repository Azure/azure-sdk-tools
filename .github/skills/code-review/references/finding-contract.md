# Finding Contract

Report a finding only when every condition holds:

1. The pull request introduced or worsened the issue.
2. The comment targets an exact changed line.
3. A concrete input, state, or call sequence reaches the failure.
4. The user or system impact is clear.
5. Nearby guards, callers, tests, and intentional patterns do not invalidate
   the concern.
6. The issue is not merely an MCP001-MCP007, compiler, formatter, linter, or
   CI result.

Do not report style preferences, pre-existing problems, generic hardening,
theoretical concerns, or missing tests without an uncovered behavioral risk.
Consolidate repeated symptoms into one root-cause finding.

Keep a finding concise: state the defect, the triggering scenario and impact,
then a safe fix direction. If any required evidence is missing, continue
investigating or omit the finding.
