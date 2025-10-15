# Dev Container Configuration

This directory contains the Dev Container configuration for developing Azure SDK Tools in GitHub Codespaces or VS Code with the Dev Containers extension.

## What's Included

The devcontainer is configured with the following tools and features:

- **.NET SDK** - For building and running C# projects
- **Node.js (LTS)** - For JavaScript/TypeScript development
- **Python 3.12** - For Python tools and scripts
- **Go** - For Go-based tools
- **Docker-in-Docker** - For building and running containers
- **Git & Git LFS** - For version control with large file support
- **GitHub CLI** - For interacting with GitHub from the command line

## Base Image

The configuration uses `mcr.microsoft.com/devcontainers/base:ubuntu` as the base image, which is:
- Actively maintained by Microsoft
- Lightweight (~100MB)
- Fast to build and start
- Compatible with all Dev Container Features

## Changes from Previous Configuration

The previous configuration used `mcr.microsoft.com/vscode/devcontainers/universal:0-linux`, which was:
- Deprecated and no longer maintained
- Very large (2GB+) causing slow build times
- Led to timeout issues when creating Codespaces

The new configuration uses Dev Container Features, which are:
- Modular and cacheable
- Faster to build
- Easier to maintain and update
- Industry standard for Dev Containers

## Usage

### GitHub Codespaces
Simply create a new Codespace from the repository, and it will automatically use this configuration.

### VS Code Dev Containers
1. Install the [Dev Containers extension](https://marketplace.visualstudio.com/items?itemName=ms-vscode-remote.remote-containers)
2. Open the repository in VS Code
3. Click "Reopen in Container" when prompted, or use the command palette: `Dev Containers: Reopen in Container`

## Customization

To add more tools or features, see the [Dev Container Features catalog](https://containers.dev/features).

Add new features to the `features` section in `devcontainer.json`.
