# Azure SDK AI Bot

This folder contains AI-powered components that work together to provide intelligent assistance for Azure SDK, including TypeSpec authoring guidance, Azure SDK generation support, and service team onboarding.

## Architecture Overview

The system consists of six main components that work together to provide support for Azure SDK domain knowledge. Each component operates independently with well-defined interfaces:

```text
┌────────────────────────────────────────────────────────────────────────────────────────────────────────────────┐
│                                            Azure SDK AI Bot System                                             │
├─────────────────────┬───────────────────────────┬────────────────────────────┬─────────────────────────────────┤
│     Teams Bot       │      Backend Service      │        Azure Function      │          Knowledge Sync         │
│    (TypeScript)     │           (Go)            │         (TypeScript)       │           (TypeScript)          │
│ azure-sdk-qa-bot/   │ azure-sdk-qa-bot-backend/ │ azure-sdk-qa-bot-function/ │ azure-sdk-qa-bot-knowledge-sync/│
│                     │                           │                            │                                 │
├─────────────────────┴───────────────────────────┴────────────────────────────┴─────────────────────────────────┤
│                  Shared Library                 │                        Evaluation Framework                  │
│                   (TypeScript)                  │                              (Python)                        │
│           azure-sdk-qa-bot-backend-shared/      │                     azure-sdk-qa-bot-evaluation/             │
│                                                 │                                                              │
└─────────────────────────────────────────────────┴──────────────────────────────────────────────────────────────┘
```

## Components

### 1. Azure SDK QA Bot (`azure-sdk-qa-bot/`)

An intelligent assistant that operates within Microsoft Teams to help developers with Azure SDK related questions. It provides real-time guidance on TypeSpec authoring, Azure SDK onboarding, and best practices by leveraging AI-powered responses.

### 2. Backend API Service (`azure-sdk-qa-bot-backend/`)

The core processing engine responsible for generating AI-powered responses for the bot. It receives user questions, processes them through AI models, manages user feedback, and logs interactions for analytics and improvement purposes.

### 3. Azure Function (`azure-sdk-qa-bot-function/`)

A serverless component that handles bot analytics and activity conversion. It processes Teams bot interactions and provides monitoring capabilities for the system.

### 4. Shared Library (`azure-sdk-qa-bot-backend-shared/`)

A common code library that provides consistent data structures, utility functions, and interface definitions used across all other components. It ensures standardization and reduces code duplication throughout the system.

### 5. Evaluation Framework (`azure-sdk-qa-bot-evaluation/`)

A quality assurance system that continuously monitors and evaluates the performance of the AI bot's responses. It runs automated tests to measure response accuracy, relevance, and quality to ensure the bot maintains high standards of assistance.

### 6. Knowledge Sync Service (`azure-sdk-qa-bot-knowledge-sync/`)

A standalone TypeScript application that processes documentation from various repositories and maintains the knowledge base. It clones repositories, processes markdown files and TypeSpec Spector test files, uploads processed content to Azure Blob Storage, and updates the Azure AI Search index. This service maintains change detection for efficient processing and serves as the primary knowledge management component for the system.

## Knowledge Sources

The bot provides intelligent responses by searching through comprehensive knowledge bases including:

- **TypeSpec Documentation**: Official TypeSpec language documentation
- **TypeSpec Azure Documentation**: Azure-specific TypeSpec patterns and practices
- **Azure SDK Engineering Hub**: Internal development guidelines and best practices
- **Custom Documentation**: Configurable additional sources via JSON configuration

## Getting Started

### Prerequisites

- **Node.js**: Version 20+
- **Go**: Version 1.23+ (for backend service)
- **Python**: Version 3.10+ (for evaluation framework)
- **Azure Subscription**: For deploying cloud resources
- **Teams Toolkit**: For Teams app development and deployment

### Local Development

#### Teams Bot

```bash
cd azure-sdk-qa-bot
npm install
npm run dev
```

#### Backend Service

```bash
cd azure-sdk-qa-bot-backend
go mod download
go run main.go
```

#### Azure Function

```bash
cd azure-sdk-qa-bot-function
npm install
npm start
```

#### Knowledge Sync Service

```bash
cd azure-sdk-qa-bot-knowledge-sync
npm install
npm start
```

#### Shared Library

```bash
cd azure-sdk-qa-bot-backend-shared
npm install
npm run dev:local
```

#### Evaluation Framework

```bash
cd azure-sdk-qa-bot-evaluation
pip install -r requirements.txt
python run.py
```

**NOTE**: Running Evaluations

To run evaluations, see: [azure-sdk-qa-bot-evaluation/README.md](./azure-sdk-qa-bot-evaluation/README.md)

## Configuration

### Documentation Sources

Add new documentation sources by updating the knowledge configuration. The Knowledge Sync Service uses `azure-sdk-qa-bot-knowledge-sync/config/knowledge-config.json`. See [Self-Serve Knowledge Sources Guide](docs/SELF_SERVE_ADD_KNOWLEDGE_SOURCES.md) for detailed instructions.

### Environment Variables

Each component requires specific environment variables for Azure service connections, API keys, and configuration settings. Refer to individual component READMEs for detailed configuration requirements.

## Support

For questions and support related to Azure SDK AI tools:

- Review component-specific READMEs for detailed documentation
- Check the [troubleshooting guide](azure-sdk-qa-bot-backend/TROUBLE_SHOOTING.md)
- Refer to the [self-serve knowledge sources guide](docs/SELF_SERVE_ADD_KNOWLEDGE_SOURCES.md)
