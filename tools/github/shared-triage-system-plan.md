# Shared Triage & Workflow System — Plan (v0)

**Status:** Early draft — high-level sketch for team discussion

**Owner:** Azure SDK DevExp / Tools team

**Last updated:** 2025-01-16

---

## Problem

Every Azure SDK language repo reinvents the same triage logic — issue classification, labeling rules, codeowner routing, lifecycle state machines. This duplicates effort, leads to inconsistent behavior across repos, and makes it hard to roll out improvements globally. Teams want automation that works for their repo but don't want to maintain the underlying triage knowledge themselves.

## Approach

We will facilitate this system by creating a set of **shared instruction files or skills** that contain the steps on things like issue triage and issue workflow. Teams create their own agent workflows for their repos and **point those to the instruction files or skills we sync out**. Then they can choose to add **customizations on top of those instructions**.

### Key components

1. **Shared skills** — Maintained centrally in `Azure/azure-sdk-tools` under `tools/github/shared-skills/`. Each skill is a self-contained directory with a `SKILL.md` (instructions agents read) and metadata. Examples: `issue-triage`, `issue-workflow`, `codeowner-routing`.

2. **Consumer repos opt in** — Each language repo (e.g., `azure-sdk-for-python`) adds a workflow that points to the shared skills. The skills are synced into the repo (likely via PRs from this repo) so they're local and reviewable.

3. **Customization layer** — Repos can add override files or workflow inputs to extend or tweak the shared behavior without forking the whole skill. Overrides are short and repo-specific ("In Python, also apply label X when Y").

4. **Monitoring & contract tests** — A centralized monitor verifies the workflows actually do what they're supposed to do (e.g., every new issue gets labeled within N hours). Contract tests run in PR checks to catch breakage before it ships.

## Monitoring & contract tests

The plan should also lay out how we will have **monitoring tools and tests to make sure the agent workflows on the repos perform the minimal required actions**. Specifically:

- **Contract assertions** — For each skill, define measurable outcomes (e.g., "new issues get service labels within 4 hours," "codeowner is pinged within 8 hours"). These are checkable via the GitHub API.
- **Monitoring job** — A scheduled workflow in this repo queries consumer repos for recent issues and checks whether the assertions hold. Emits results to a dashboard and opens issues when thresholds are breached.
- **PR-time tests** — A reusable workflow that spins up synthetic issues, runs the consumer's triage workflow, and asserts the contract. Required check for PRs that modify skills or workflows.

## Open questions

- **Sync mechanism:** How do skills get from this repo to consumers? Push PRs, pull at runtime, or pinned URL references? Likely a hybrid (push for installed copy, pins for explicit versioning).
- **Skill format:** Markdown only, or markdown + frontmatter/metadata sidecar? What versioning scheme?
- **Consumer registry:** How do we track which repos have opted in? YAML file, GitHub topic, inferred from workflows?
- **Failure modes:** If the agent runner is down, do issues stay untriaged (fail open) or do we escalate (fail closed)?

## Next steps

1. Get feedback on this plan — is the shape right?
2. Author one skill (`issue-triage`) end-to-end as the reference implementation.
3. Pick one pilot consumer repo (e.g., Python). Wire up the sync, workflow, and one override. Run on real issues for 2 weeks.
4. Stand up the monitoring job against the pilot. Verify it catches failures.

If that works, expand to a second skill and a second repo. If it doesn't, revise the plan.
