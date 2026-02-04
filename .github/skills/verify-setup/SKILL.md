---
name: verify-setup
description: Verifies that the environment is set up with the required installations for SDK development, and running MCP release tools. Use at the start of interaction, before using any azsdk MCP tools, when a tool fails, or when user asks about their setup. 
---

# Verify Setup

ALWAYS verify the core requirements. Then, choose to verify language requirements based on the user's working repo, or their explicit request. 

Run the 'Check Command' for each requirement, and chain together commands when possible for efficiency. If missing, provide 'Installation Instructions' based on the user's platform.

When a specific path is needed for an installation instruction, find it for the user and provide the exact command they can run.

## Core Requirements

| Requirement | Check Command | Min Version | Purpose | Auto Install | Installation Instructions |
|-------------|---------------|-------------|---------|--------------|--------------------------|
| Node.js | `node --version` | 22.16.0 | JavaScript runtime | false | **Linux:** `sudo apt install nodejs`<br>**Windows/macOS:** Download from https://nodejs.org |
| tsp-client | **Language repos:** `npm exec --prefix eng/common/tsp-client --no -- tsp-client --version`<br>**Specs repo:** `tsp-client --version` | 0.24.0 | TypeSpec client tooling | true | **Language repos:** `cd eng/common/tsp-client && npm ci`<br>**Specs repo:** `cd <repo-root> && npm ci` |
| tsp | `tsp --version` | 1.0.0 | TypeSpec compiler | false | **Language repos:** `npm install -g @typespec/compiler@latest`<br>**Specs repo:** `cd <repo-root> && npm ci` |
| PowerShell | `pwsh --version` | 7.0 | Scripting and automation | false | Download and install from https://learn.microsoft.com/powershell/scripting/install/install-powershell |
| GitHub CLI | `gh --version` | 2.30.0 | GitHub integration | false | Download and install from https://cli.github.com/ |
| Git long paths | `git config --get core.longpaths` | - | Windows-only: enables long file paths | false | **Windows:** `git config --global core.longpaths true`<br>Check Registry: LongPathsEnabled=1 in HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\FileSystem |
| Python | `python --version` | 3.9 | Required for Verify-Readme script (all repos) | false | **Linux:** `sudo apt install python3 python3-pip python3-venv && sudo apt install python-is-python3`<br>**Windows/macOS:** Download from https://www.python.org/downloads/ |
| pip | `python -m pip --version` | - | Required for Verify-Readme script (all repos) | false | **Linux:** `sudo apt install python3-pip`<br>**Windows/macOS:** `python -m ensurepip` |

## Language-Specific Requirements

- [Python](references/verify-python.md)
- [Java](references/verify-java.md)
- [JavaScript/TypeScript](references/verify-javascript.md)
- [.NET](references/verify-dotnet.md)
- [Go](references/verify-go.md)

## Handling Results

### Success Response
All requirements are installed. The environment is ready for Python SDK development.

### Failure Response

1. Summarize missing requirement information
2. If the user wants help installing missing requirements, execute the 'Install Command' for requirements where auto-install is true. For others, explain why you cannot install (system-level installations will not be auto installed)
