# Spec: 1-env-setup - verify-setup

## Table of Contents

- [Definitions](#definitions)
- [Background / Problem Statement](#background--problem-statement)
- [Goals and Exceptions/Limitations](#goals-and-exceptionslimitations)
- [Design Proposal](#design-proposal)
- [Alternatives Considered](#alternatives-considered)
- [Open Questions](#open-questions)
- [Implementation Plan](#implementation-plan)
- [Testing Strategy](#testing-strategy)

---

## Definitions

**Environment Setup**: The process of ensuring all required tools, SDKs, and dependencies are installed and properly configured for SDK development work.

**Requirements**: Language-specific and core tools needed to successfully run Dev Inner Loop MCP tools (e.g., TypeSpec compiler, language SDKs, build tools).

**Verification Check**: A command or script that determines whether a specific requirement is installed and accessible in the developer's environment.

**Lazy Loading**: Checking only the requirements for the language(s) currently being worked on, rather than checking all languages upfront.

**Source of Truth**: A single, authoritative configuration that defines what requirements are needed for each language and how to install them.

---

## Background / Problem Statement

### Current State

**Current state per language:**

- **.NET**: Developers manually discover missing dependencies when build or generation commands fail. No proactive verification.
- **Java**: Same as .NET - reactive discovery through failures.
- **JavaScript**: Same as .NET - reactive discovery through failures.
- **Python**: Some repos have `azpysdk` which can be checked, but no unified verification across all required tools.
- **Go**: Same as .NET - reactive discovery through failures.

**Data plane vs. Management plane:**

Both data plane and management plane SDK development currently lack proactive environment verification. TypeSpec-based generation (common in data plane) and Swagger-based generation (common in management plane) each have their own tooling requirements, but neither has automated verification.

### Why This Matters

Without proactive environment verification:

1. **Poor Developer Experience**: Developers waste time debugging cryptic error messages that are actually due to missing tools
2. **Friction for New Contributors**: New team members or open-source contributors struggle to get started
3. **MCP Agent Limitations**: When the agent attempts to run tools and they fail due to missing dependencies, the agent can't distinguish between actual errors and setup issues
4. **Context Switching Pain**: When developers work across multiple language repos, they must manually remember and install different requirements for each language

**Agent vs. Manual Workflow:**

- **With Agent (Current)**: Agent attempts a command → fails → developer must interpret error → manually install missing tool → retry
- **With Agent (Proposed)**: Agent checks setup first → proactively tells developer what's missing and how to fix it → developer installs → proceed with confidence
- **Manual CLI (Current)**: Developer runs command → fails → searches documentation or asks teammates → installs tool → retry
- **Manual CLI (Proposed)**: Developer explicitly runs `azsdk verify-setup` → sees clear list of missing items → follows install instructions → proceeds

This tool bridges the gap and makes the agent workflow much more reliable.

---

## Goals and Exceptions/Limitations

### Goals

What are we trying to achieve with this design?

- [ ] Proactively verify that required tools are installed before running Dev Inner Loop operations
- [ ] Provide clear, actionable installation instructions when requirements are missing
- [ ] Support language-specific requirement checking (lazy loading based on current context)
- [ ] Work seamlessly in both MCP (agent) and CLI execution modes
- [ ] Minimize developer friction when working across multiple language repositories
- [ ] Establish a foundation for dynamic requirement discovery in future versions

### Exceptions and Limitations

_Known cases where this approach doesn't work or has limitations._

#### Exception 1: Hard-Coded Requirements (V1 Limitation)

**Description:**

In V1, requirements and installation instructions are hard-coded in the tool rather than derived from a dynamic source of truth (e.g., devcontainer configs, CI definitions).

**Impact:**

- Maintenance burden: When tools or versions change, the code must be updated
- Potential drift: Hard-coded requirements may become outdated if not regularly maintained
- No single source of truth across repos

**Workaround:**

For V1, this is acceptable to prove the concept. V2 will address dynamic requirement discovery (see Open Questions).

---

#### Exception 2: Installation Instruction Specificity

**Description:**

Some installation steps may be complex or environment-specific (e.g., Windows vs. Linux, different shell configurations, corporate proxy settings).

**Impact:**

Generic instructions may not work for all developer environments, requiring developers to adapt instructions to their context.

**Workaround:**

- Provide instructions that work for the most common scenarios
- Rely on Copilot agent to help interpret and adapt instructions when needed
- Link to comprehensive documentation for complex cases

---

#### Exception 3: Cross-Language Requirement Conflicts

**Description:**

If a developer requests verification for multiple languages (e.g., `--langs python,java`) and one language's requirements fail, it's unclear whether to:

- Block the entire operation
- Continue checking remaining languages and report all failures

**Impact:**

Developer experience depends on this decision - blocking is safer but less informative, while continuing provides more information but may be confusing.

**Workaround:**

**Proposed behavior**: Continue checking all requested languages and report all missing requirements together. This gives developers a complete picture of what needs to be fixed.

---

#### Language-Specific Limitations

| Language   | Limitation | Impact | Workaround |
|------------|------------|--------|------------|
| .NET       | Multiple .NET SDK versions may be installed; need to verify correct version | May pass check but still fail with version mismatch | Check for specific version in verification command |
| Java       | Multiple JDK installations; Maven vs. Gradle detection | Similar version/tooling mismatch | Check `JAVA_HOME` and verify Maven availability |
| JavaScript | Multiple Node versions via nvm; npm vs. yarn | Version mismatch possible | Check Node version explicitly |
| Python     | Virtual environment must be activated; multiple Python versions | Tool may not be accessible if venv not activated | Instructions explicitly mention venv activation |
| Go         | Multiple Go versions via version managers | Version mismatch possible | Check Go version explicitly |

---

## Design Proposal

### Overview

The `verify-setup` tool checks whether required dependencies for SDK development are installed in the developer's environment. It operates in two modes:

1. **Automatic**: Runs when MCP server starts, checking requirements for the detected language
2. **Explicit**: Developer or agent explicitly invokes the tool with specific language(s)

The tool uses a hard-coded registry of requirements (V1) that includes:

- What to check for each requirement
- How to verify it's installed
- Instructions for installing if missing

### Detailed Design

#### Requirements Registry Structure

The requirements are organized by language with a shared "core" section:

```json
{
  "core": [
    {
      "name": "Git",
      "check": ["git", "--version"],
      "install_instructions": [
        "Install Git from https://git-scm.com/downloads",
        "Verify installation with: git --version"
      ],
      "required_for": "Version control operations"
    },
    {
      "name": "PowerShell 7+",
      "check": ["pwsh", "-Version"],
      "install_instructions": [
        "Install PowerShell 7+ from: https://aka.ms/install-powershell",
        "Verify installation with: pwsh -Version"
      ],
      "required_for": "Running eng/common scripts"
    }
  ],
  "python": [
    {
      "name": "Python 3.8+",
      "check": ["python", "--version"],
      "install_instructions": [
        "Install Python 3.8 or later from https://www.python.org/downloads/",
        "Verify installation with: python --version"
      ],
      "required_for": "Python SDK development"
    },
    {
      "name": "azpysdk",
      "check": ["azpysdk", "--help"],
      "install_instructions": [
        "1. Navigate to your azure-sdk-for-python repo",
        "2. Ensure your virtual environment is activated",
        "3. Run: python -m pip install -e eng/tools/azure-sdk-tools[build]",
        "4. Verify installation with: azpysdk --help"
      ],
      "required_for": "Python SDK build and packaging"
    }
  ],
  "java": [
    {
      "name": "Java JDK 11+",
      "check": ["java", "-version"],
      "install_instructions": [
        "Install Java JDK 11+ from https://adoptium.net/",
        "Set JAVA_HOME environment variable",
        "Verify installation with: java -version"
      ],
      "required_for": "Java SDK development"
    },
    {
      "name": "Maven",
      "check": ["mvn", "--version"],
      "install_instructions": [
        "Install Maven from https://maven.apache.org/download.cgi",
        "Verify installation with: mvn --version"
      ],
      "required_for": "Java SDK build and packaging"
    }
  ],
  "javascript": [
    {
      "name": "Node.js 18+",
      "check": ["node", "--version"],
      "install_instructions": [
        "Install Node.js 18+ from https://nodejs.org/",
        "Or use nvm: nvm install 18",
        "Verify installation with: node --version"
      ],
      "required_for": "JavaScript SDK development"
    },
    {
      "name": "npm",
      "check": ["npm", "--version"],
      "install_instructions": [
        "npm is typically installed with Node.js",
        "If missing, reinstall Node.js from https://nodejs.org/",
        "Verify installation with: npm --version"
      ],
      "required_for": "JavaScript package management"
    }
  ],
  "dotnet": [
    {
      "name": ".NET SDK 6.0+",
      "check": ["dotnet", "--version"],
      "install_instructions": [
        "Install .NET SDK 6.0+ from https://dotnet.microsoft.com/download",
        "Verify installation with: dotnet --version"
      ],
      "required_for": ".NET SDK development"
    }
  ],
  "go": [
    {
      "name": "Go 1.20+",
      "check": ["go", "version"],
      "install_instructions": [
        "Install Go 1.20+ from https://go.dev/doc/install",
        "Verify installation with: go version"
      ],
      "required_for": "Go SDK development"
    }
  ],
  "typespec": [
    {
      "name": "TypeSpec Compiler",
      "check": ["tsp", "--version"],
      "install_instructions": [
        "Install TypeSpec globally: npm install -g @typespec/compiler",
        "Verify installation with: tsp --version"
      ],
      "required_for": "TypeSpec-based SDK generation"
    }
  ]
}
```

#### CLI Command

**Command**: `azsdk verify-setup`

**Parameters**:

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `--langs` | string[] | No | Current repo language | Comma-separated list of languages to verify (e.g., `python,java`) |
| `--all` | flag | No | false | Check requirements for all languages |
| `--include-typespec` | flag | No | false | Also check TypeSpec requirements (useful for TypeSpec-based generation) |

**Examples**:

```bash
# Check requirements for current repo's language (detected automatically)
azsdk verify-setup

# Check Python requirements explicitly
azsdk verify-setup --langs python

# Check multiple languages
azsdk verify-setup --langs python,java

# Check all languages
azsdk verify-setup --all

# Check Python and TypeSpec (for TypeSpec-based SDK generation)
azsdk verify-setup --langs python --include-typespec
```

#### MCP Tool Invocation

**Tool Name**: `verify-setup`

**MCP Parameters**:

```json
{
  "languages": ["python", "java"],
  "includeTypeSpec": false
}
```

**When Automatically Invoked**:

1. **MCP Server Start**: Automatically check requirements for detected language when server starts
2. **Before Generation**: When user requests SDK generation, verify requirements first
3. **On Failure**: When another tool fails due to missing dependencies, suggest running verify-setup

### Cross-Language Considerations

| Language   | Approach | Notes |
|------------|----------|-------|
| .NET       | Check `dotnet` CLI availability and version | May need to verify specific SDK version |
| Java       | Check `java`, `javac`, and `mvn` | Must verify `JAVA_HOME` is set |
| JavaScript | Check `node` and `npm` versions | Consider `nvm` for version management |
| Python     | Check `python` and `azpysdk` in venv context | Critical: venv must be activated |
| Go         | Check `go` version | Straightforward check |

### User Experience

#### Success Case (All Requirements Met)

```bash
$ azsdk verify-setup --langs python

✅ Verifying Python environment setup...

All requirements are installed:
  ✅ Python 3.8+
  ✅ azpysdk

Your environment is ready for Python SDK development!
```

#### Failure Case (Missing Requirements)

```bash
$ azsdk verify-setup --langs python,java

⚠️  Verifying Python and Java environment setup...

Python Requirements:
  ✅ Python 3.8+
  ❌ azpysdk - MISSING

Java Requirements:
  ✅ Java JDK 11+
  ❌ Maven - MISSING

---

To fix missing requirements:

📦 azpysdk:
   1. Navigate to your azure-sdk-for-python repo
   2. Ensure your virtual environment is activated
   3. Run: python -m pip install -e eng/tools/azure-sdk-tools[build]
   4. Verify installation with: azpysdk --help

📦 Maven:
   1. Install Maven from https://maven.apache.org/download.cgi
   2. Verify installation with: mvn --version

After installing, run 'azsdk verify-setup --langs python,java' again to verify.
```

#### MCP Agent Interaction

**User**: "Check my environment setup"

**Agent**: _Runs verify-setup for current repo language_

```text
I've checked your Python environment setup. You're missing one requirement:

❌ azpysdk

To install it:
1. Make sure your virtual environment is activated
2. Run: python -m pip install -e eng/tools/azure-sdk-tools[build]
3. Verify with: azpysdk --help

Would you like me to help you run these commands?
```

### Architecture Diagram

```text
┌─────────────────────────────────────────────────┐
│  Developer / Agent                               │
└──────────────────┬──────────────────────────────┘
                   │
                   │ "Check setup" / azsdk verify-setup
                   ▼
┌─────────────────────────────────────────────────┐
│  verify-setup Tool                               │
│  ┌───────────────────────────────────────────┐  │
│  │ 1. Determine Languages to Check           │  │
│  │    - Parse --langs parameter              │  │
│  │    - Or detect from current repo          │  │
│  └───────────────┬───────────────────────────┘  │
│                  │                               │
│  ┌───────────────▼───────────────────────────┐  │
│  │ 2. Load Requirements Registry            │  │
│  │    - Core requirements                    │  │
│  │    - Language-specific requirements       │  │
│  └───────────────┬───────────────────────────┘  │
│                  │                               │
│  ┌───────────────▼───────────────────────────┐  │
│  │ 3. For Each Requirement:                 │  │
│  │    - Run check command                    │  │
│  │    - Capture result (installed/missing)   │  │
│  │    - Track missing items                  │  │
│  └───────────────┬───────────────────────────┘  │
│                  │                               │
│  ┌───────────────▼───────────────────────────┐  │
│  │ 4. Format Results                         │  │
│  │    - List installed requirements          │  │
│  │    - List missing requirements            │  │
│  │    - Include install instructions         │  │
│  └───────────────┬───────────────────────────┘  │
└──────────────────┼──────────────────────────────┘
                   │
                   ▼
┌─────────────────────────────────────────────────┐
│  Output                                          │
│  - Success: "All requirements met"               │
│  - Failure: List of missing items + instructions │
└─────────────────────────────────────────────────┘
```

---

## Alternatives Considered

### Alternative 1: Dynamic Requirements from Devcontainer Config

**Description:**

Instead of hard-coding requirements, parse them from `.devcontainer/devcontainer.json` files in each language repo.

**Pros:**

- Single source of truth (devcontainer config)
- Automatically stays in sync with container definitions
- Leverages existing infrastructure

**Cons:**

- Devcontainer configs may not be complete or consistent across repos
- Requires parsing different formats (some repos use Docker images, others use features)
- Installation instructions not typically included in devcontainer configs
- Adds complexity for a V1 tool

**Why not chosen:**

For V1, we want to prove the concept with minimal dependencies. V2 can explore dynamic discovery once we understand the usage patterns.

---

### Alternative 2: Check Requirements Only When Other Tools Fail

**Description:**

Don't provide a standalone verify-setup tool. Instead, when other tools fail (e.g., generate-sdk), detect if it's an environment issue and suggest specific fixes.

**Pros:**

- No separate tool needed
- More contextual (only checks what's needed for the failing operation)
- Simpler for developers (fewer commands to learn)

**Cons:**

- Reactive instead of proactive
- Developers can't proactively check their setup before starting work
- Each tool would need to implement error detection logic
- Harder to onboard new contributors who want to verify setup upfront

**Why not chosen:**

A proactive verification tool provides better developer experience, especially for onboarding and when working across multiple languages.

---

### Alternative 3: Integration with Devcontainers CLI

**Description:**

Use the devcontainers CLI tool to build and validate container images locally, ensuring the environment matches CI.

**Pros:**

- Guarantees environment matches CI/CD
- Comprehensive solution

**Cons:**

- Requires Docker and devcontainers CLI installed
- Much heavier solution (downloading/building containers)
- Not all developers want to work in containers
- Slower feedback loop

**Why not chosen:**

Too heavy for a V1 environment verification tool. This could be a future option for developers who want container-based development.

---

## Open Questions

- [ ] **Question 1**: How should we handle dynamic requirement discovery in V2?
  - Context: Hard-coding requirements in V1 works but isn't maintainable long-term
  - Options:
    - Parse `.devcontainer/devcontainer.json` files from each repo
    - Use CI pipeline definitions (e.g., `ci.yml`) as source of truth
    - Create a new eng/common config file specifically for dev requirements
    - Leverage devcontainers CLI to interrogate container definitions
  - **Proposal**: Explore eng/common config file that each repo can customize, with a standard schema

- [ ] **Question 2**: Should installation instructions be in copilot-instructions.md instead?
  - Context: Rich had suggested putting PowerShell installation instructions in language-specific copilot-instructions files
  - Options:
    - Keep instructions in the tool (easier to maintain centrally)
    - Move to copilot-instructions.md per language (more discoverable by agent)
    - Hybrid: Basic instructions in tool, detailed instructions in docs with links
  - **Proposal**: Start with instructions in the tool for V1. If Copilot can't handle the specificity, we'll move to copilot-instructions.md

- [ ] **Question 3**: How do we detect if a tool failure is an environment issue vs. a real error?
  - Context: When generate-sdk fails, should we automatically suggest running verify-setup?
  - Options:
    - Parse error output for common environment-related error messages
    - Always suggest verify-setup on any tool failure (simpler but noisy)
    - Don't auto-suggest; only run verify-setup when explicitly requested
  - **Proposal**: For V1, don't auto-suggest. Let developers or agent explicitly invoke verify-setup when they suspect environment issues.

- [ ] **Question 4**: Should we support custom/repo-specific requirements?
  - Context: Some repos may have unique tooling needs beyond common requirements
  - Options:
    - Support a repo-local config file that extends base requirements
    - Only support central hard-coded requirements (simpler)
    - Let repos add their own copilot-instructions for unique tools
  - **Proposal**: V1 only supports central requirements. Evaluate repo-specific needs in V2.

---

## Implementation Plan

### Phase 1: Core Verification (V1)

- **Milestone**: Basic environment verification with hard-coded requirements
- **Timeline**: 2-3 weeks
- **Tasks**:
  - Define requirements registry structure
  - Implement language detection logic (reuse existing LanguageResolver)
  - Implement requirement checking logic (run commands, capture results)
  - Format output with installation instructions
  - Add MCP tool wrapper
  - Add CLI command

### Phase 2: MCP Integration

- **Milestone**: Auto-run on MCP server start
- **Timeline**: 1 week
- **Tasks**:
  - Hook verify-setup into MCP server startup
  - Add telemetry for verification results
  - Test with agent in different language repos

### Phase 3: Enhanced Output & Docs

- **Milestone**: Improved UX and documentation
- **Timeline**: 1 week
- **Tasks**:
  - Improve output formatting (colors, icons, clear sections)
  - Add documentation to README
  - Create examples and troubleshooting guide
  - Add copilot-instructions examples for agent usage

### Phase 4 (Future): Dynamic Discovery

- **Milestone**: Requirements derived from source of truth config
- **Timeline**: TBD (V2)
- **Dependencies**: Decisions on Open Questions 1 and 4

---

## Testing Strategy

### Unit Tests

- Test requirement checking logic with mock commands
- Test language detection from repo paths
- Test output formatting with various pass/fail scenarios
- Test parameter parsing (--langs, --all, --include-typespec)

### Integration Tests

- Test actual requirement checking with real commands
- Test in environments with missing dependencies
- Test across different operating systems (Windows, macOS, Linux)
- Test with virtual environments (Python) and version managers (nvm, gvm)

### Manual Testing

**Test Scenarios**:

1. **Clean Environment**: Run in fresh environment with no tools installed
   - Expected: All requirements reported as missing with instructions

2. **Partial Installation**: Install some but not all requirements
   - Expected: Correctly identify which are installed vs. missing

3. **Multi-Language**: Run with `--langs python,java`
   - Expected: Check requirements for both languages, report combined results

4. **Current Repo Detection**: Run without parameters in different language repos
   - Expected: Automatically detect and check correct language

5. **MCP Server Startup**: Start MCP server in each language repo
   - Expected: Auto-verify requirements and inform user of any issues

6. **Agent Interaction**: Ask agent "check my environment setup"
   - Expected: Agent invokes tool and presents results naturally

### Cross-Language Validation

Test in each language repository:

- [ ] azure-sdk-for-python
- [ ] azure-sdk-for-java
- [ ] azure-sdk-for-js
- [ ] azure-sdk-for-net
- [ ] azure-sdk-for-go

Verify:

- Language detection works correctly
- Requirements are appropriate for each language
- Installation instructions are accurate and complete

---

## Related Links

- [Tool Issue: VerifySetup tool for V1 · Issue #12287](https://github.com/Azure/azure-sdk-tools/issues/12287)
- [azsdkcli Issue: verifySetup · Issue #11837](https://github.com/Azure/azure-sdk-tools/issues/11837)
- [Python MCP Implementation Reference](https://github.com/Azure/azure-sdk-for-python/blob/ad3e09230bfbf31030c375102ecef366f4bcb35a/eng/tools/mcp/azure-sdk-python-mcp/main.py)

---

## Meeting Notes

### 10/9 Discussion - Key Takeaways

1. **Source of Truth**: Agreement that there should be one source-of-truth config (from eng/common or each repo) for all requirements
   - Consider devcontainers as potential source
   - May need devcontainers CLI tool to build container images

2. **Installation Instructions**: Concern over hard-coding detailed installation instructions
   - Should first check if Copilot can handle it automatically
   - Richard suggested lang-specific copilot-instructions.md (similar to PowerShell installation approach)

3. **Lazy Loading**: Agreement on "lazy loading" approach
   - Only check requirements for current language being developed
   - Don't block the developer with requirements for languages they're not using
   - Example: Dev in Python repo starts MCP → check only Python requirements

4. **Tool vs. Feature Question**: Discussion whether this needs to be standalone tool
   - Alternative: When other MCP tools fail, they return specific installation instructions
   - Decided: Standalone tool is valuable for proactive checking and onboarding

5. **Failure Detection**: How to detect environment issues vs. real errors?
   - Could scan output for common environment-related error messages
   - Need to distinguish between "tool not installed" and "tool ran but had an error"

### Action Items from Meeting

- [ ] Prototype with lazy loading based on current repo
- [ ] Test how well Copilot handles specificity of installation instructions
- [ ] Evaluate if instructions should go in copilot-instructions.md or stay in tool
