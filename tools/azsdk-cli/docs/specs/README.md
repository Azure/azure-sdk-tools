# Dev Inner Loop Design Specs

This directory contains design specifications for the Azure SDK Dev Inner Loop tools and processes. All major design proposals, feature additions, and process changes should be documented and reviewed here.

## Purpose

Design specs serve several important purposes:

- **Transparency**: Make design decisions visible to all stakeholders across SDK language teams
- **Collaboration**: Enable structured feedback and discussion from .NET, Java, JavaScript, Python, and Go teams
- **Historical Record**: Preserve context, rationale, and trade-offs made during the design process
- **Alignment**: Ensure cross-language agreement before implementation begins

## Spec Review Process

### 1. Create Your Design Spec

1. Copy the [spec-template.md](./spec-template.md) to create a new spec file
2. Name your spec using the format: `<stage-number-and-name>-<tool-name>.spec.md`
   - Each spec must be prefixed with the appropriate stage number and name
   - See [Dev Inner Loop Stages](#dev-inner-loop-stages) below for the complete list
   - Example: `1-env-setup-env-verification.spec.md`
3. Fill out all sections of the template (see [What to Include](#what-to-include) below)

#### Dev Inner Loop Stages

Use these stage prefixes when naming your spec files:

| Stage # | Stage Name | Prefix | Example Spec Name |
|---------|------------|--------|-------------------|
| 0 | Scenarios | `0-scenario` | `0-scenario-1.spec.md` |
| 1 | Environment Setup | `1-env-setup` | `1-env-setup-env-verification.spec.md` |
| 2 | Generating | `2-generating` | `2-generating-typespec-sdk.spec.md` |
| 3 | Customizing | `3-customizing` | `3-customizing-typespec-validation.spec.md` |
| 4 | Testing | `4-testing` | `4-testing-unit-test-runner.spec.md` |
| 5 | Samples | `5-samples` | `5-samples-sample-generator.spec.md` |
| 6 | Package | `6-package` | `6-package-metadata-updater.spec.md` |
| 7 | Validating | `7-validating` | `7-validating-breaking-changes.spec.md` |
| 99 | Operations | `8-operations` | `8-operations-telemetry-strategy.spec.md` |

**Notes:**

- **Stage 0 (Scenarios)**: End-to-end scenarios that span multiple stages
- **Stage 3 (Customizing)**: Covers customizing TypeSpec or SDK libraries
- **Stage 6 (Package)**: Covers package metadata and documentation updates
- **Stage 99 (Operations)**: For architecture-level or cross-cutting concerns between inner and outer loop

### 2. Open a Pull Request

1. Create a new branch for your spec
2. Add your completed spec file to this `docs/specs/` directory
3. Open a PR with a clear title like: `[Spec] Environment Setup Verification Design`
4. Tag relevant stakeholders and language representatives as reviewers:
   - **.NET**: @dotnet-driver
   - **Java**: @java-driver
   - **JavaScript**: @js-driver
   - **Python**: @python-driver
   - **Go**: @go-driver

### 3. Gather Feedback and Obtain Sign-Off

- Team members will use the PR review process to comment on and discuss the spec
- Each SDK language team should provide sign-off via **PR approval** (management plane and data plane)
- Once all required approvals are received, the spec is considered approved

### 4. Merge and Implement

- Merge the spec PR to `main`
- The merged spec serves as the authoritative design document
- Link to the merged spec from implementation PRs for context

## What to Include

Every spec should include the following sections (see [spec-template.md](./spec-template.md) for the full template):

### Required Sections

1. **Title**
   - Spec identifier and descriptive title

2. **Definitions**
   - Clear definitions of any terms that might be ambiguous
   - Examples: "What does 'generate SDK' mean in this context?"
   - Establish shared understanding before diving into design

3. **Background / Problem Statement**
   - What problem are we solving?
   - Why is this important?
   - What's the current state (per language, and for both data plane and management plane if they differ)?

4. **Goals and Exceptions/Limitations**
   - What are we trying to achieve?
   - Known cases where this approach doesn't work or has limitations

5. **Design Proposal**
   - Detailed explanation of the proposed solution
   - Architecture diagrams, code samples, or workflows as appropriate
   - How will this work across different SDK languages?

6. **Open Questions**
   - Unresolved items that need discussion
   - Areas where input is specifically needed

7. **Success Criteria**
   - Measurable criteria that define when the feature/tool is complete

8. **Agent Prompts**
   - Natural language prompts users can provide to the AI agent (GitHub Copilot) with expected agent behavior

9. **CLI Commands**
   - Direct command-line usage examples with options and expected outputs

10. **Implementation Plan**
    - Phasing, milestones, dependencies

11. **Testing Strategy**
    - How will this be validated?

12. **Documentation Updates**
    - What docs need to change?

13. **Metrics/Telemetry**
    - What data should we collect?

### Optional Sections (as relevant)

- **Alternatives Considered**: What other approaches were evaluated? Why was this design chosen over alternatives?

## Examples

Example specs will be added here as they are created and approved.

## Tips for Writing Good Specs

- **Start with Definitions**: Don't assume everyone interprets terms the same way
- **Use Definition Links**: When using defined terms throughout your spec, link to their definitions using anchor tags (e.g., `[preview release](#preview-release)`)
- **Be Specific**: Vague proposals lead to vague feedback
- **Show, Don't Just Tell**: Include diagrams, code samples, or examples
- **Include Agent Prompts**: For user-facing tools, document natural language prompts that should work in agent mode
- **Document CLI Usage**: Provide concrete CLI command examples with all options and expected outputs
- **Define Success Criteria**: Be explicit about what "done" looks like with measurable criteria
- **Anticipate Questions**: Address obvious concerns preemptively in your spec
- **Document Exceptions**: If your design doesn't work for a specific case, say so explicitly
- **Keep It Current**: Update the spec as discussions evolve
- **Cross-Language Thinking**: Consider how the design impacts all SDK languages

## When to Write a Spec

Write a spec for:

- **New tools or features** in the Dev Inner Loop toolkit
- **Process changes** that affect multiple teams
- **Architectural decisions** that have long-term implications
- **Design patterns** that should be followed consistently
- **Telemetry and metrics** we want to collect
- **Breaking changes** to existing tools or workflows

You don't need a spec for:

- Bug fixes that don't change behavior
- Minor refactoring or code cleanup
- Documentation updates (unless they reflect process changes)
- Obvious improvements with no trade-offs

## Questions?

If you're unsure whether your proposal needs a spec, or have questions about the review process, reach out to the Dev Inner Loop team lead or ask in the Dev Inner Loop Teams channel.

---

_This process was established in October 2025 to improve design collaboration across Azure SDK language teams._
