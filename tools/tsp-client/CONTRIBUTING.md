# Contributing to tsp-client

Thank you for your interest in contributing to the TypeSpec Client Generator CLI (`tsp-client`)! This tool facilitates generating client libraries from TypeSpec specifications.

## Table of Contents

- [Getting Started](#getting-started)
- [Development Setup](#development-setup)
- [Project Structure](#project-structure)
- [Development Workflow](#development-workflow)
- [Testing](#testing)
- [Code Style and Linting](#code-style-and-linting)
- [Debugging](#debugging)
- [Contributing Guidelines](#contributing-guidelines)
- [Submitting Changes](#submitting-changes)
- [Reporting Issues](#reporting-issues)

## Getting Started

This project welcomes contributions and suggestions. Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## Development Setup

### Prerequisites

- [Node.js 18.19 LTS](https://nodejs.org/en/download/) or later (Node.js 20.6.0+ also supported)
- npm (comes with Node.js)
- Git

### Installation

1. Fork and clone the repository:

   ```bash
   git clone https://github.com/your-username/azure-sdk-tools.git
   cd azure-sdk-tools/tools/tsp-client
   ```

2. Install dependencies:

   ```bash
   npm install
   ```

3. Build the project:

   ```bash
   npm run build
   ```

4. Link the package for local development (optional):
   ```bash
   npm link
   ```

After linking, you can use `tsp-client` commands globally on your system during development.

## Project Structure

```
tsp-client/
├── src/                    # Source code
│   ├── commands.ts         # Command implementations
│   ├── index.ts           # CLI entry point
│   ├── fs.ts              # File system utilities
│   ├── git.ts             # Git operations
│   ├── log.ts             # Logging utilities
│   ├── network.ts         # Network operations
│   ├── npm.ts             # NPM operations
│   ├── typespec.ts        # TypeSpec operations
│   └── utils.ts           # General utilities
├── test/                   # Tests
│   ├── *.spec.ts          # Unit tests
│   ├── examples/          # Test examples
│   └── utils/             # Test utilities
├── cmd/                    # CLI wrapper script
├── dist/                   # Compiled output (generated)
├── package.json           # Package configuration
├── tsconfig.json          # TypeScript configuration
├── vitest.config.ts       # Test configuration
├── README.md              # Usage documentation
└── CONTRIBUTING.md        # This file
```

## Development Workflow

### Available Scripts

- `npm run build` - Clean and build the project
- `npm run build:tsc` - Build without cleaning
- `npm run clean` - Remove compiled output
- `npm run purge` - Remove node_modules and package-lock.json
- `npm test` - Run all tests
- `npm run test:commands` - Run the run_commands.ts script for testing
- `npm run watch` - Build in watch mode for development
- `npm run format` - Format code using Prettier
- `npm run format:check` - Check code formatting

### Development Process

1. **Create a feature branch:**

   ```bash
   git checkout -b feature/your-feature-name
   ```

2. **Make your changes:**
   - Edit source files in the `src/` directory
   - Add tests for new functionality in the `test/` directory
   - Update documentation as needed

3. **Test your changes:**

   ```bash
   npm test
   npm run format:check
   ```

4. **Build:**
   ```bash
   npm run build
   ```

## Testing

### Running Tests

```bash
# Run all tests
npm test

# Run specific test file
npm test -- commands.spec.ts

# Run command integration tests
npm run test:commands
```

### Writing Tests

- Place unit tests in the `test/` directory with `.spec.ts` extension
- Use the existing test utilities in `test/utils/`
- Follow the existing test patterns for consistency
- Test both success and error scenarios
- Mock external dependencies appropriately

### Test Categories

- **Unit tests**: Test individual functions and modules
- **Command tests**: Test CLI command functionality
- **Integration tests**: Test end-to-end workflows

## Code Style and Linting

This project uses [Prettier](https://prettier.io/) for code formatting.

### Code Style Guidelines

- Follow existing naming conventions:
  - `camelCase` for variables and functions
  - `PascalCase` for classes and interfaces
  - `kebab-case` for file names
- Add JSDoc comments for public APIs
- Use meaningful variable and function names
- Keep functions focused and small when possible

### Formatting

```bash
# Format all files
npm run format

# Check formatting without making changes
npm run format:check
```

The project uses these Prettier settings (see `.prettierrc.json`):

- 2-space indentation
- Single quotes for strings
- Semicolons required
- Trailing commas where valid

## Debugging

### Local Development

1. Build the project: `npm run build`
2. Use the built executable: `npx tsx src/index.js [command] [options]`

### Using Debugger

For VS Code debugging:

1. Set breakpoints in TypeScript source files
2. Select the "Javascript Debug Terminal" from the VS Code terminal options
3. Run with tsx for direct TypeScript debugging:
   ```bash
   npx tsx src/index.ts [command] [options]
   ```

### Logging

The tool includes built-in logging. Use the logging utilities in `src/log.ts` for consistent output:

```typescript
import { log } from "./log.js";

log.info("Information message");
log.warn("Warning message");
log.error("Error message");
```

## Contributing Guidelines

### Types of Contributions

We welcome several types of contributions:

1. **Bug fixes** - Fix issues in existing functionality
2. **Feature enhancements** - Add new features or improve existing ones
3. **Documentation improvements** - Improve README, code comments, or examples
4. **Performance improvements** - Optimize existing code
5. **Test coverage** - Add or improve tests

### Before You Start

1. Check existing [issues](https://github.com/Azure/azure-sdk-tools/issues) and [pull requests](https://github.com/Azure/azure-sdk-tools/pulls)
2. For large changes, consider opening an issue first to discuss the approach
3. Ensure your development environment is set up correctly

### Coding Standards

- Write clear, readable code with appropriate comments
- Follow TypeScript best practices
- Ensure backward compatibility when possible
- Add tests for new functionality
- Update documentation for user-facing changes

## Submitting Changes

### Pull Request Process

1. **Fork the repository** and create your branch from `main`

2. **Make your changes** following the guidelines above

3. **Test thoroughly:**

   ```bash
   npm test
   npm run format:check
   npm run build
   ```

4. **Commit your changes** with clear, descriptive commit messages.

5. **Push to your fork:**

   ```bash
   git push origin feature/your-feature-name
   ```

6. **Create a Pull Request** with:
   - Clear title and description
   - Reference to related issues (if any)
   - Summary of changes made
   - Any breaking changes noted

### Pull Request Guidelines

- Keep changes focused and atomic
- Include tests for new functionality
- Update documentation as needed
- Ensure all CI checks pass
- Respond promptly to review feedback

## Reporting Issues

### Bug Reports

When reporting bugs, please include:

1. **Clear title and description** of the issue
2. **Steps to reproduce** the problem
3. **Expected vs. actual behavior**
4. **Environment information:**
   - Node.js version (`node --version`)
   - npm version (`npm --version`)
   - Operating system
   - tsp-client version (`tsp-client --version`)
5. **Error messages or logs** (if applicable)
6. **Sample TypeSpec files** or configurations (if relevant)

### Feature Requests

For feature requests, please provide:

1. **Clear description** of the proposed feature
2. **Use case or problem** it would solve
3. **Proposed API or interface** (if applicable)
4. **Examples** of how it would be used

### Getting Help

- Check the [README.md](./README.md) for usage instructions
- Search existing [issues](https://github.com/Azure/azure-sdk-tools/issues)
- For questions about TypeSpec, refer to the [TypeSpec documentation](https://typespec.io/)

## Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft
trademarks or logos is subject to and must follow
[Microsoft's Trademark & Brand Guidelines](https://www.microsoft.com/en-us/legal/intellectualproperty/trademarks/usage/general).
Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship.
Any use of third-party trademarks or logos are subject to those third-party's policies.
