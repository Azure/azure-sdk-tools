# Pylint MCP Server

A Model Context Protocol (MCP) server for running Pylint code analysis.

## Overview

This MCP server provides a way to run Pylint analysis on Python code through the Model Context Protocol. It offers tools to analyze Python code and report linting issues in a structured JSON format.

## Features

- Run Pylint analysis on files or directories
- Get Pylint version information
- Configure analysis with various options like fast mode and file-only mode
- JSON-formatted output for easy integration with other tools

## Installation

```bash
pip install -r requirements.txt
```

## Usage

### Starting the server

```bash
python server.py
```

By default, the server uses stdio transport. You can change this by setting the `MCP_TRANSPORT` environment variable:

```bash
MCP_TRANSPORT=http python server.py
```

### Available Tools

The server provides the following MCP tools:

#### get_pylint

Analyze Python code with Pylint.

Parameters:
- `path` (string): Absolute path to the file or directory to analyze

#### get_pylint_version

Get the installed version of Pylint.

## Example Client Usage

```python
import json
from mcp.client import Client

# Connect to the MCP server
client = Client("lint", transport="stdio")

# Run Pylint on a file
result = client.get_pylint(path="/path/to/your/module.py", fast_mode=True)
linting_results = json.loads(result)

# Get Pylint version
version_info = client.get_pylint_version()
```

## License

See the repository license information.