# Azure SDK AI Bot

This folder contains AI-powered components that work together to provide intelligent assistance for Azure SDK development, including TypeSpec authoring guidance, Azure SDK generation support, and service team onboarding.

## Architecture Overview

The system consists of five main components working together to provide intelligent assistance for Azure SDK development:

```text
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Teams Bot     │    │   Go Backend    │    │  Azure Function │
│  (TypeScript)   │◄──►│   API Service   │◄──►│   (TypeScript)  │
└─────────────────┘    └─────────────────┘    └─────────────────┘
         │                       │                       │
         ▼                       ▼                       ▼
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│  Shared Library │    │   Evaluation    │    │ Knowledge Base  │
│  (TypeScript)   │    │   (Python)      │    │ (Configurable)  │
└─────────────────┘    └─────────────────┘    └─────────────────┘
```

## Components

### 1. Azure SDK QA Bot (`azure-sdk-qa-bot/`)

An intelligent assistant that operates within Microsoft Teams to help developers with Azure SDK related questions. It provides real-time guidance on TypeSpec authoring, SDK generation, and best practices by leveraging AI-powered responses.

### 2. Backend API Service (`azure-sdk-qa-bot-backend/`)

The core processing engine that handles AI-powered response generation for the bot. It receives user questions, processes them through AI models, manages user feedback, and logs interactions for analytics and improvement purposes.

### 3. Azure Function (`azure-sdk-qa-bot-function/`)

A serverless component responsible for maintaining and updating the knowledge base that powers the bot's responses. It processes documentation from various sources, indexes content for searchability, and manages the configuration of knowledge sources.

### 4. Shared Library (`azure-sdk-qa-bot-backend-shared/`)

A common code library that provides consistent data structures, utility functions, and interface definitions used across all other components. It ensures standardization and reduces code duplication throughout the system.

### 5. Evaluation Framework (`azure-sdk-qa-bot-evaluation/`)

A quality assurance system that continuously monitors and evaluates the performance of the AI bot's responses. It runs automated tests to measure response accuracy, relevance, and quality to ensure the bot maintains high standards of assistance.

## Knowledge Sources

The bot provides intelligent responses by searching through comprehensive knowledge bases including:

- **TypeSpec Documentation**: Official TypeSpec language documentation
- **TypeSpec Azure Documentation**: Azure-specific TypeSpec patterns and practices
- **Azure SDK Engineering Hub**: Internal development guidelines and best practices
- **Custom Documentation**: Configurable additional sources via JSON configuration

## Getting Started

### Prerequisites

- **Node.js**: Version 18, 20, or 22
- **Go**: Version 1.19+ (for backend service)
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
func start
```

#### Evaluation Framework

```bash
cd azure-sdk-qa-bot-evaluation
pip install -r requirements.txt
python evaluation.py
```

## Configuration

### Documentation Sources

Add new documentation sources by updating `azure-sdk-qa-bot-function/config/knowledge-config.json`. See [Self-Serve Knowledge Sources Guide](docs/SELF_SERVE_ADD_KNOWLEDGE_SOURCES.md) for detailed instructions.

### Environment Variables

Each component requires specific environment variables for Azure service connections, API keys, and configuration settings. Refer to individual component READMEs for detailed configuration requirements.

## Support

For questions and support related to Azure SDK AI tools:

- Review component-specific READMEs for detailed documentation
- Check the [troubleshooting guide](azure-sdk-qa-bot-backend/TROUBLE_SHOOTING.md)
- Refer to the [self-serve knowledge sources guide](docs/SELF_SERVE_ADD_KNOWLEDGE_SOURCES.md)
