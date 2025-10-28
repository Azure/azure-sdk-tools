# Self-Serve Add Knowledge Sources

This document provides a guide for developers to add new knowledge sources to the Azure SDK QA Bot by updating the configuration and submitting a pull request.

## Overview

The Azure SDK QA Bot uses a configuration-driven approach to manage knowledge sources. Developers can add new documentation repositories and sources by updating the `azure-sdk-qa-bot-knowledge-sync/config/knowledge-config.json` file and following the process outlined in this document.

## Prerequisites

- Understanding of the documentation source you want to add
- Valid GitHub/Azure DevOps repository URL for the documentation source
- Only support markdown files, if files are not in markdown format, you need to transform them to markdown format

## Configuration Structure

The knowledge sources are defined in `azure-sdk-qa-bot-knowledge-sync/config/knowledge-config.json`. Each source contains:

- **Repository configuration**: Git repository details, authentication, and access settings
- **Documentation paths**: Specific paths within the repository that contain documentation
- **Processing options**: File naming conventions, ignored paths, and folder mappings

### Configuration Schema

The complete knowledge source configuration schema is defined at `azure-sdk-qa-bot-knowledge-sync/config/knowledge-config.schema.json`

Example:
```json
{
  "$schema": "./knowledge-config.schema.json",
  "description": "Unified configuration for knowledge sources",
  "version": "1.0.0",
  "sources": [
    {
      "repository": {
        "url": "https://github.com/owner/repo.git",
        "path": "optional-local-path",
        "branch": "main",
        "authType": "public"
      },
      "paths": [
        {
          "name": "unique_source_name",
          "description": "Description of this documentation source",
          "path": "docs/folder",
          "folder": "target_folder_name",
          "fileNameLowerCase": true,
          "ignoredPaths": ["folder-to-ignore"]
        }
      ]
    }
  ]
}
```

## Step-by-Step Process

### 1. Identify Your Knowledge Source

Before adding a configuration, gather the following information:

- **Repository URL**: The Git repository containing your documentation
- **Branch**: The branch to use (typically `main` or `master`)
- **Documentation paths**: Specific folders within the repo that contain relevant docs
- **Authentication type**: Whether the repository is public, requires SSH, or token authentication
- **Target audience**: Which teams, tenants, or channels will benefit from this knowledge source

### 2. Determine Authentication Type

Choose the appropriate authentication type based on your repository:

#### Public Repository
```json
{
  "url": "https://github.com/microsoft/typespec.git",
  "branch": "main",
  "authType": "public"
}
```

#### Private Repository with SSH(need env config)
```json
{
  "url": "git@github-microsoft:owner/private-repo.git",
  "branch": "main",
  "authType": "ssh",
  "sshHost": "github-microsoft"
}
```

#### Private Repository with Token(need env config)
```json
{
  "url": "https://dev.azure.com/azure-sdk/internal/_git/docs.git",
  "branch": "main",
  "authType": "token",
  "tokenEnvVar": "AZURE_SDK_ENG_HUB_TOKEN"
}
```

### 3. Configure Documentation Paths

For each documentation folder you want to include, add a path configuration:

```json
{
  "name": "unique_identifier",
  "description": "Human-readable description of the content",
  "path": "relative/path/in/repo",
  "folder": "target_folder_name",
  "fileNameLowerCase": true,
  "ignoredPaths": ["internal", "private", "temp"]
}
```

#### Path Configuration Options

- **`name`** (required): Unique identifier for this documentation source
- **`description`** (required): Clear description of what this documentation contains
- **`path`** (optional): Relative path within the repository. If omitted, uses the repository root
- **`folder`** (optional): Target folder name for processed documentation
- **`fileNameLowerCase`** (optional): Convert filenames to lowercase (default: false)
- **`ignoredPaths`** (optional): Array of folder/file patterns to ignore during processing

### 4. Add Your Configuration

1. **Fork the repository** or create a new branch
2. **Edit** `/config/knowledge-config.json`
3. **Add your source** to the `sources` array
4. **Validate** the configuration against the schema

#### Example Addition

```json
{
  "repository": {
    "url": "https://github.com/your-team/your-docs.git",
    "branch": "main",
    "authType": "public"
  },
  "paths": [
    {
      "name": "your_team_api_docs",
      "description": "API documentation for Your Team's services",
      "path": "docs/api",
      "folder": "your-team-api-docs",
      "fileNameLowerCase": true,
      "ignoredPaths": ["internal", "drafts"]
    },
    {
      "name": "your_team_tutorials",
      "description": "Getting started tutorials and guides",
      "path": "docs/tutorials",
      "folder": "your-team-tutorials",
      "fileNameLowerCase": true
    }
  ]
}
```

### 5. Create a Pull Request

When submitting your pull request, include the following information:

#### Pull Request Template

**Title**: `[Teams Chatbot]: Add knowledge source: [Your Documentation Name]`
**Labels**: `Teams Chatbot`, `AI Projects`, `Knowledge Base`

**Description**:
```markdown
## Knowledge Source Addition

### Documentation Source Details
- **Repository**: [Repository URL]
- **Target Channels**: [TypeSpec Discussion Channel, Language-Python, etc.]

### 6. Validation and Testing

Before submitting your PR, ensure:

1. **Schema Validation**: Your configuration validates against `knowledge-config.schema.json`
2. **Repository Access**: The bot can access your repository with the specified authentication
3. **Path Verification**: The documentation paths exist and contain relevant content
4. **No Sensitive Data**: Ensure no internal or sensitive information is in public paths
5. **Proper Authentication**: If using private repositories, ensure proper environment variables are configured

#### Local Validation

You can validate your configuration locally:

```bash
1. Add a new source constant at https://github.com/wanlwanl/wanl-fork-azure-sdk-tools/blob/azure-sdk-ai-bot/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/model/completion.go
2. Add the new source to the tenant config: https://github.com/wanlwanl/wanl-fork-azure-sdk-tools/blob/azure-sdk-ai-bot/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/config/tenant.go
3. Follow the README, test locally: https://github.com/wanlwanl/wanl-fork-azure-sdk-tools/blob/azure-sdk-ai-bot/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/README.md
```

#### Test Channel Validation

Contact with the Teams Chat Bot developer to help deploy this change to [Dev environment](https://github.com/wanlwanl/wanl-fork-azure-sdk-tools/blob/azure-sdk-ai-bot/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/TROUBLE_SHOOTING.md#development-environment), and test if knowledge source has been added successfully.