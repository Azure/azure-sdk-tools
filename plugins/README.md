# Plugins

This directory contains agent plugins for Azure SDK development.

## Installation

### Via GitHub Copilot CLI

1. Add this repository as a plugin marketplace:

    ```bash
    copilot plugin marketplace add Azure/azure-sdk-tools
    ```

2. Install the desired plugin:

    ```bash
    copilot plugin install azure-sdk-tools@azure-sdk-plugins
    ```

3. Verify installation:

    ```bash
    copilot plugin list
    ```

### Via VS Code

1. Open the Command Palette (`Ctrl+Shift+P` / `Cmd+Shift+P` on Mac).
2. Run `Chat: Install Plugin from Source`.
3. Enter the marketplace repository: `Azure/azure-sdk-tools`.
4. Select the `azure-sdk-tools` plugin from the list.

## Available Plugins

### azure-sdk-tools

A plugin for Azure SDK development workflows including SDK generation, build, test, release planning, and pipeline management. It bundles MCP server configurations and reusable skills for AI-assisted development agents.

See [azure-sdk-tools/skills/README.md](azure-sdk-tools/skills/README.md) for the full list of skills and development details.
