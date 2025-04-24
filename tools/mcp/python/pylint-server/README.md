# Pylint MCP Server

A Model Context Protocol (MCP) server for running Pylint code analysis.

## Overview

This MCP server provides a way to run Pylint analysis on Python code through the Model Context Protocol. It offers tools to analyze Python code and report linting issues in a structured JSON format.

## Features

- Run Pylint analysis on files or directories
- JSON-formatted output for easy integration with other tools

## Installation

```bash
pip install -r requirements.txt
```

## Usage

### Starting the server

```bash
python lint.py
```

By default, the server uses stdio transport. 

### Available Tools

The server provides the following MCP tools:

#### get_pylint

Analyze Python code with Pylint.

Parameters:
- `path` (string): Absolute path to the file or directory to analyze
