# APIView Documentation

## Contents

| Document | Description |
|----------|-------------|
| [user-guide.md](docs/user-guide.md) | Guide for SDK authors and reviewers |
| [ci-integration.md](docs/ci-integration.md) | CI/release pipeline behavior |
| [troubleshooting.md](docs/troubleshooting.md) | FAQ — access, uploads, CI, releases |
| [CONTRIBUTING.md](CONTRIBUTING.md) | Contributor setup and development |
| [overview.md](docs/overview.md) | Architecture and internals |
| [operations.md](docs/operations.md) | Ops, deployment, and configuration |
| [release-approval.md](docs/release-approval.md) | Approval and release gating |
| [legacy.md](docs/legacy.md) | Legacy Razor Pages system |
| [sandboxing.md](docs/sandboxing.md) | Sandboxed parser execution |

---

## File Index

### [overview.md](docs/overview.md) — Architecture Overview
Full system architecture: backend, frontend, data model, token format, diff algorithm, workflows, CI pipelines, and file paths.
- 1\. What Is APIView?
- 2\. Solution Structure
- 3\. Data Model Hierarchy
- 4\. Backend Architecture (ASP.NET Core, controllers, managers, data stores, background services, integrations)
- 5\. Frontend Architecture (Angular SPA: routes, components, services, backend communication)
- 6\. Token File Format (flat vs tree, fields, token kinds, ID system, content hashing)
- 7\. Diff Pipeline
- 8\. Approval & Release Flow *(details in [release-approval.md](docs/release-approval.md))*
- 9\. Language Parsers
- 10\. Core Workflows (CI Automatic, CI Pull Request, Manual upload — with sequence diagrams)
- 11\. SDK CI Pipelines (per-language pipeline chain)
- 12\. Key File Paths

### [user-guide.md](docs/user-guide.md) — User Guide
For SDK authors, architects, and reviewers using APIView day-to-day.
- What Is APIView?
- Key Concepts (Review, API Revision, revision types)
- Getting Access
- API Approvals (first release vs GA, who can approve)
- Review Process (comment severity, comment source)
- Navigating the Review Page (command bar, revisions panel)

### [ci-integration.md](docs/ci-integration.md) — How Reviews, Revisions, and Pipelines Work
When and why API revisions are created, when approvals are required, and why pipelines fail.
- Background
- Key Concept: API Surface, Not Versions
- Types of API Revisions (PR-based, automatic/scheduled)
- Release Enforcement Logic (trigger conditions, GA vs pre-release)
- Common Scenarios (FAQ for PR/release issues)
- Design Principles

### [release-approval.md](docs/release-approval.md) — API Approval & Release Gating
Complete approval workflow: prerequisites, toggle flow, carry-forward, release gating endpoints.
- 1\. Approval Levels (revision, review-level, namespace)
- 2\. GA vs Preview Version Classification
- 3\. Approval Prerequisites
- 4\. Approval Toggle Flow
- 5\. Automatic Approval Carry-Forward
- 6\. Release Gating / CI/CD Integration (endpoint, response codes, resolution logic)
- 7\. Marking a Revision as Released
- 8\. End-to-End Lifecycle

### [troubleshooting.md](docs/troubleshooting.md) — Troubleshooting (User FAQ)
Common issues and solutions for APIView users.
- Access (org visibility, CorpNet)
- Uploads & Parsing (Java, Python)
- CI & Revisions
- Releases & Approvals (why blocked, revision types, key vault errors)
- Get Help

### [operations.md](docs/operations.md) — Operations Guide
Deployment, test environments, configuration, and engineering team troubleshooting.
- Service Overview (prod/staging URLs, Azure Portal links)
- Deployment (staging slot, verification, swap)
- Test Environment (UX test instance, deploy steps)
- Configuration (approvers, Copilot review required)
- Troubleshooting (cross-references troubleshooting.md)

### [CONTRIBUTING.md](CONTRIBUTING.md) — Contributor Guide
Developer setup and workflow for contributing to APIView.
- Prerequisites
- Staging Environment Permissions
- Local Setup (GitHub OAuth, user secrets, build and run)
- Making Changes (where to modify, how to test, submitting PRs)
- Logs and Monitoring

### [legacy.md](docs/legacy.md) — Legacy Razor Pages System
The deprecated Razor Pages frontend: what remains, format routing, and migration path.
- 1\. Background
- 2\. Token Format: Flat vs Tree (ParserStyle enum, routing logic)
- 3\. Which Languages Use Which Format (modern vs legacy tables)
- 4\. Razor Pages Inventory (review pages, infrastructure pages, shared partials)
- 5\. Legacy MVC Controllers
- 6\. Legacy HTML Rendering Pipeline
- 7\. Legacy Static Assets
- 8\. Coexistence Model (Razor/SPA routing handoff)
- 9\. Migration Path (per-language effort estimates)

### [sandboxing.md](docs/sandboxing.md) — Sandboxing (Offline API Review Generation)
Deprecated pattern: running parsers in Azure DevOps pipelines instead of on the server.
- 1\. What Is Sandboxing?
- 2\. Languages That Use Sandboxing (Python, TypeSpec, Swagger)
- 3\. How It Works (overview, sequence diagram, batch regeneration)
- 4\. Key Code Locations
- 5\. App Configuration Keys
- 6\. Known Limitations & Issues
- 7\. Implications for Future Work
