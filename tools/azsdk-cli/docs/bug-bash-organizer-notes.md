# Bug Bash Organizer Notes

Companion to [bug-bash-guide.md](./bug-bash-guide.md). The participant guide is the source of truth for *what* to test and *how* to file feedback. This file holds the organizer-only logistics so they don't clutter the participant doc.

## Scope and goals

**What we're testing:** the Azure SDK Tools Agent (`azsdk-cli`) in three modes — standalone CLI, MCP server in VS Code Copilot, and GitHub Coding Agent in Actions.

**In scope:** command correctness, MCP tool invocations, error handling, integration with the SDK language repos and `azure-rest-api-specs`, end-to-end TypeSpec → SDK workflow, release plan workflows, APIView, pipeline diagnostics, doc clarity.

**Out of scope:** Azure service backends (APIView server, ADO infra), language SDK runtime behavior unless directly caused by generated code, non-agent `eng/common` tooling.

**Success looks like:**
- Each major tool/command exercised by ≥3 different participants
- 10+ actionable bugs / improvements logged
- 5+ documented UX friction points
- All unclear/missing docs noted
- Failure-path coverage, not just happy paths

**Track:** participant count, hours of testing, issues filed by severity/category, command coverage map, time-to-complete per scenario, scenario success rate.

## Invite list

**Internal (Microsoft):** 2–4 SDK engineers per language (.NET / Java / JS / Python), 1–2 service team reps, 1–2 EngSys, 1–2 PMs, plus REST API specs reviewers and tools maintainers. Send a calendar invite (2–4 hour window or full day) with the participant guide link, the feedback channel, and the Teams channel for live coordination. Post in the Azure SDK Teams channels and the `azure-sdk-tools` repo discussions.

**External:** service teams already using TypeSpec, MCP early adopters, community contributors, partners. Reach via repo announcement, blog/newsletter, direct outreach, or convert an office-hours session. For external participants, confirm test-resource access and NDA boundaries; route their feedback through a separate channel so internal-only info doesn't leak.

**Team size:** 5–8 minimum for coverage, 15–20 optimal, 30 max before you need a dedicated coordinator.

## Before / during / after

**Before**
- Send reminders 1 week and 1 day out with the setup checklist
- Run through every scenario yourself first to catch broken setup steps
- Have sample TypeSpec projects, test repos, example commands pre-staged
- Assign 1–2 coordinators to be available live

**During**
- Watch the chat channel for common blockers and broadcast fixes
- Encourage real-time sharing of interesting findings
- Recognize good bug reports publicly to keep momentum
- Be responsive — quick answers keep the session productive

**After**
- Don't let issues languish: triage within 48 hours
- Send weekly fix-progress updates to participants
- If multiple participants hit the same issue, prioritize it

## Post-bash triage

### Within 24 hours

1. Thank participants with summary stats; recognize top contributors
2. Pull all `bug-bash`-labeled issues, export to a spreadsheet
3. Triage `severity:critical` items immediately — assign owners, communicate ETAs

### Within 1 week

4. Categorize and dedupe; tag by component (TypeSpec, release plan, pipeline, etc.)
5. Prioritize using severity + impact:
   - **P0:** blocks core scenarios, data loss, security
   - **P1:** major functionality broken, no workaround
   - **P2:** significant inconvenience, workaround exists
   - **P3:** minor / cosmetic
6. Assign owners and milestones
7. Group documentation feedback and route to PM/docs owner — target one sprint

### Within 1 sprint

8. Land P0/P1 fixes in the next release; add regression tests
9. Update docs for every gap identified; update *this* guide too
10. Review enhancement requests against roadmap; respond in the issue either way

### Follow-up

11. Publish results — blog or repo discussion: participation stats, top issues, what got fixed, roadmap shifts. Thank participants publicly.
12. Comment on every issue with disposition (fixed / planned / deferred / won't-fix), link the PR, ask reporter to verify

### Effectiveness review

13. Compare against success criteria. Calculate:
    - Issue discovery rate (issues / participant-hour)
    - Fix rate within one sprint
    - Scenario coverage (% attempted)
    - Participant satisfaction
14. Document lessons learned. Feed into the next bug bash.

## Cadence

- **Pre-release:** always before major version releases
- **Quarterly:** for actively developed surface area
- **After major changes:** when new tools or workflows ship

## Document metadata

- **Version:** 1.0
- **Last updated:** 2025-01-24
- **Maintained by:** Azure SDK Tools PM Team
