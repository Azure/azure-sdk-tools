#!/usr/bin/env python3
"""
MCP Server for Azure SDK QA Bot

This MCP server provides tools to:
1. Review SDK code against Azure SDK guidelines
2. Ask questions about TypeSpec and Azure SDK development
"""

import json
import httpx
from mcp.server import Server
from mcp.server.stdio import stdio_server
from mcp.types import Tool, TextContent

# Configuration
CODE_REVIEW_API_URL = "http://localhost:8088/code_review"
COMPLETION_API_URL = "http://localhost:8088/completion"

# Fixed tenant for TypeSpec QA bot
TENANT_ID = "azure_sdk_qa_bot"

server = Server("azure-sdk-qa-bot")


@server.list_tools()
async def list_tools() -> list[Tool]:
    """List available tools."""
    return [
        Tool(
            name="review_sdk_code",
            description="""Review SDK code against Azure SDK guidelines.
            
This tool analyzes code to check if it follows Azure SDK design guidelines for the specified language.
It returns comments about potential guideline violations with suggestions for fixes.

Supported languages: go, python, java, javascript, dotnet
            """,
            inputSchema={
                "type": "object",
                "properties": {
                    "language": {
                        "type": "string",
                        "description": "The programming language of the code (e.g., go, python, java, javascript, dotnet)",
                        "enum": ["go", "python", "java", "javascript", "dotnet"]
                    },
                    "code": {
                        "type": "string",
                        "description": "The code content to review"
                    },
                    "file_path": {
                        "type": "string",
                        "description": "Optional: The relative file path of the code (helps identify file type for appropriate guidelines)",
                        "default": ""
                    }
                },
                "required": ["language", "code"]
            }
        ),
        Tool(
            name="ask_typespec_question",
            description="""Ask questions about TypeSpec for Azure API specifications.
            
This tool provides answers to questions about:
- TypeSpec language syntax and features
- TypeSpec decorators and built-in types
- Azure TypeSpec libraries (@azure-tools/typespec-azure-core, @azure-tools/typespec-azure-resource-manager, etc.)
- API specification authoring with TypeSpec
- TypeSpec project configuration and setup
- Migrating from Swagger/OpenAPI to TypeSpec

The tool uses RAG (Retrieval-Augmented Generation) to provide accurate answers based on official TypeSpec documentation.
            """,
            inputSchema={
                "type": "object",
                "properties": {
                    "message": {
                        "type": "string",
                        "description": "The question to ask about TypeSpec or Azure SDK development"
                    },
                    "history": {
                        "type": "array",
                        "description": "Optional: Previous conversation history for context",
                        "items": {
                            "type": "object",
                            "properties": {
                                "role": {
                                    "type": "string",
                                    "enum": ["user", "assistant"],
                                    "description": "The role of the message sender"
                                },
                                "content": {
                                    "type": "string",
                                    "description": "The content of the message"
                                }
                            },
                            "required": ["role", "content"]
                        },
                        "default": []
                    },
                    "additional_context": {
                        "type": "array",
                        "description": "Optional: Additional context such as links, images, or text to help answer the question",
                        "items": {
                            "type": "object",
                            "properties": {
                                "type": {
                                    "type": "string",
                                    "enum": ["link", "image", "text"],
                                    "description": "The type of additional information"
                                },
                                "content": {
                                    "type": "string",
                                    "description": "The content (text or base64 for images)"
                                },
                                "link": {
                                    "type": "string",
                                    "description": "The URL if type is 'link'"
                                }
                            },
                            "required": ["type", "content"]
                        },
                        "default": []
                    }
                },
                "required": ["message"]
            }
        )
    ]


@server.call_tool()
async def call_tool(name: str, arguments: dict) -> list[TextContent]:
    """Handle tool calls."""
    if name == "review_sdk_code":
        return await handle_code_review(arguments)
    elif name == "ask_typespec_question":
        return await handle_typespec_question(arguments)
    else:
        return [TextContent(type="text", text=f"Unknown tool: {name}")]


async def handle_code_review(arguments: dict) -> list[TextContent]:
    """Handle code review tool calls."""
    language = arguments.get("language", "")
    code = arguments.get("code", "")
    file_path = arguments.get("file_path", "")

    if not language:
        return [TextContent(type="text", text="Error: 'language' is required")]
    if not code:
        return [TextContent(type="text", text="Error: 'code' is required")]

    # Prepare request payload
    payload = {
        "language": language,
        "code": code
    }
    if file_path:
        payload["file_path"] = file_path

    try:
        async with httpx.AsyncClient(timeout=120.0) as client:
            response = await client.post(
                CODE_REVIEW_API_URL,
                json=payload,
                headers={"Content-Type": "application/json"}
            )
            response.raise_for_status()
            result = response.json()

    except httpx.TimeoutException:
        return [TextContent(type="text", text="Error: Request timed out. The code review API may be slow or unavailable.")]
    except httpx.HTTPStatusError as e:
        return [TextContent(type="text", text=f"Error: HTTP {e.response.status_code} - {e.response.text}")]
    except httpx.RequestError as e:
        return [TextContent(type="text", text=f"Error: Failed to connect to code review API at {CODE_REVIEW_API_URL}. Is the server running?\n{str(e)}")]
    except json.JSONDecodeError:
        return [TextContent(type="text", text="Error: Invalid JSON response from API")]

    # Format the response
    output = format_review_result(result)
    return [TextContent(type="text", text=output)]


async def handle_typespec_question(arguments: dict) -> list[TextContent]:
    """Handle TypeSpec question tool calls."""
    message = arguments.get("message", "")
    history = arguments.get("history", [])
    additional_context = arguments.get("additional_context", [])

    if not message:
        return [TextContent(type="text", text="Error: 'message' is required")]

    # Build the completion request payload
    payload = {
        "tenant_id": TENANT_ID,
        "message": {
            "role": "user",
            "content": message
        }
    }

    # Add history if provided
    if history:
        payload["history"] = history

    # Add additional info if provided
    if additional_context:
        payload["additional_infos"] = additional_context

    try:
        async with httpx.AsyncClient(timeout=180.0) as client:
            response = await client.post(
                COMPLETION_API_URL,
                json=payload,
                headers={"Content-Type": "application/json"}
            )
            response.raise_for_status()
            result = response.json()

    except httpx.TimeoutException:
        return [TextContent(type="text", text="Error: Request timed out. The completion API may be slow or unavailable.")]
    except httpx.HTTPStatusError as e:
        return [TextContent(type="text", text=f"Error: HTTP {e.response.status_code} - {e.response.text}")]
    except httpx.RequestError as e:
        return [TextContent(type="text", text=f"Error: Failed to connect to completion API at {COMPLETION_API_URL}. Is the server running?\n{str(e)}")]
    except json.JSONDecodeError:
        return [TextContent(type="text", text="Error: Invalid JSON response from API")]

    # Format the response
    output = format_completion_result(result)
    return [TextContent(type="text", text=output)]
    return [TextContent(type="text", text=output)]


def format_review_result(result: dict) -> str:
    """Format the code review result into a readable string."""
    lines = []
    
    review_id = result.get("id", "N/A")
    language = result.get("language", "N/A")
    summary = result.get("summary", "")
    comments = result.get("comments", [])

    lines.append(f"# Code Review Results")
    lines.append(f"**Review ID:** {review_id}")
    lines.append(f"**Language:** {language}")
    lines.append(f"**Summary:** {summary}")
    lines.append("")

    if not comments:
        lines.append("✅ No guideline violations detected. Code looks good!")
    else:
        lines.append(f"## Found {len(comments)} Issue(s):\n")
        
        for i, comment in enumerate(comments, 1):
            lines.append(f"### Issue {i}")
            lines.append("")
            
            bad_code = comment.get("bad_code", "")
            suggestion = comment.get("suggestion")
            description = comment.get("comment", "")
            guideline_id = comment.get("guideline_id", "")
            guideline_link = comment.get("guideline_link", "")
            guideline_content = comment.get("guideline_content", "")

            lines.append(f"**Problem:** {description}")
            lines.append("")
            lines.append(f"**Bad Code:**")
            lines.append(f"```")
            lines.append(bad_code)
            lines.append(f"```")
            lines.append("")
            
            if suggestion:
                lines.append(f"**Suggested Fix:**")
                lines.append(f"```")
                lines.append(suggestion)
                lines.append(f"```")
                lines.append("")
            
            if guideline_link:
                lines.append(f"**Guideline:** [{guideline_id}]({guideline_link})")
            elif guideline_id:
                lines.append(f"**Guideline ID:** {guideline_id}")
            
            if guideline_content:
                lines.append(f"**Guideline Excerpt:**")
                lines.append(f"> {guideline_content[:500]}{'...' if len(guideline_content) > 500 else ''}")
            
            lines.append("")
            lines.append("---")
            lines.append("")

    return "\n".join(lines)


def format_completion_result(result: dict) -> str:
    """Format the completion result into a readable string."""
    lines = []
    
    answer = result.get("answer", "")
    has_result = result.get("has_result", False)
    references = result.get("references", [])

    if not has_result:
        lines.append("I couldn't find a specific answer to your question.")
        lines.append("")
    
    if answer:
        lines.append(answer)
        lines.append("")

    if references:
        lines.append("## References")
        lines.append("")
        for ref in references:
            title = ref.get("title", "Untitled")
            link = ref.get("link", "")
            if link:
                lines.append(f"- [{title}]({link})")
            else:
                lines.append(f"- {title}")
        lines.append("")

    return "\n".join(lines)


async def main():
    """Run the MCP server."""
    async with stdio_server() as (read_stream, write_stream):
        await server.run(
            read_stream,
            write_stream,
            server.create_initialization_options()
        )


if __name__ == "__main__":
    import asyncio
    asyncio.run(main())
