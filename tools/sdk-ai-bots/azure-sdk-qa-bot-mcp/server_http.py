#!/usr/bin/env python3
"""
HTTP-based MCP Server for Azure SDK QA Bot

This server exposes the MCP protocol over HTTP/SSE for deployment on Azure App Service.
It supports Microsoft Entra ID authentication and forwards requests to the backend API.

Provides tools for:
1. Code review against Azure SDK guidelines
2. Answering TypeSpec-related questions
"""

import os
import json
import asyncio
import logging
import uuid
from typing import Optional, Dict
from contextlib import asynccontextmanager

import httpx
from fastapi import FastAPI, HTTPException, Request
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import StreamingResponse, JSONResponse
from azure.identity import DefaultAzureCredential, ManagedIdentityCredential
from azure.core.credentials import AccessToken

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger(__name__)

# Configuration from environment
CODE_REVIEW_API_URL = os.getenv(
    "CODE_REVIEW_API_URL",
    "https://azuresdkqabot-dev-serve-codereview-ahefg8gpdxhngah0.westus2-01.azurewebsites.net/code_review"
)
COMPLETION_API_URL = os.getenv(
    "COMPLETION_API_URL",
    "https://azuresdkqabot-dev-serve-codereview-ahefg8gpdxhngah0.westus2-01.azurewebsites.net/completion"
)
BACKEND_CLIENT_ID = os.getenv("BACKEND_CLIENT_ID", "api://azure-sdk-qa-bot-dev")
AZURE_CLIENT_ID = os.getenv("AZURE_CLIENT_ID")  # User-assigned managed identity client ID
PORT = int(os.getenv("PORT", "8000"))

# Fixed tenant for TypeSpec QA bot
TENANT_ID = "azure_sdk_qa_bot"

http_client: Optional[httpx.AsyncClient] = None
credential: Optional[DefaultAzureCredential] = None
message_queues: Dict[str, asyncio.Queue] = {}


def _interesting_headers(headers) -> Dict[str, str]:
    """Return non-sensitive headers that help with debugging MCP clients"""
    interesting = {}
    for key, value in headers.items():
        lower_key = key.lower()
        if "mcp" in lower_key or "session" in lower_key:
            interesting[key] = value
    return interesting


def _build_external_url(path: str, request: Request) -> str:
    """Construct fully-qualified URL respecting proxy headers"""
    scheme = request.headers.get("x-forwarded-proto", request.url.scheme)
    host = request.headers.get("x-forwarded-host") or request.headers.get("host")
    if not host:
        host = request.url.hostname or "localhost"
    if not path.startswith("/"):
        path = f"/{path}"
    return f"{scheme}://{host}{path}"


@asynccontextmanager
async def lifespan(app: FastAPI):
    """Manage application lifecycle"""
    global http_client, credential

    logger.info("Starting Azure SDK QA Bot MCP Server")
    logger.info(f"Listening on port: {PORT}")
    logger.info(f"Code Review API URL: {CODE_REVIEW_API_URL}")
    logger.info(f"Completion API URL: {COMPLETION_API_URL}")
    logger.info(f"Backend Client ID: {BACKEND_CLIENT_ID}")
    if AZURE_CLIENT_ID:
        logger.info(f"User-Assigned Managed Identity Client ID: {AZURE_CLIENT_ID}")

    http_client = httpx.AsyncClient(timeout=120.0)

    try:
        if os.getenv("WEBSITE_INSTANCE_ID"):
            if AZURE_CLIENT_ID:
                credential = ManagedIdentityCredential(client_id=AZURE_CLIENT_ID)
                logger.info("Using User-Assigned Managed Identity for authentication")
            else:
                credential = ManagedIdentityCredential()
                logger.info("Using System-Assigned Managed Identity for authentication")
        else:
            credential = DefaultAzureCredential()
            logger.info("Using DefaultAzureCredential for authentication")
    except Exception as e:
        logger.warning(f"Failed to initialize Azure credential: {e}")
        credential = None

    try:
        yield
    finally:
        logger.info("Shutting down Azure SDK QA Bot MCP Server")
        if http_client:
            await http_client.aclose()


app = FastAPI(
    title="Azure SDK QA Bot MCP Server",
    description="MCP Server for reviewing SDK code and answering TypeSpec questions",
    version="1.0.0",
    lifespan=lifespan
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


async def get_backend_token() -> Optional[str]:
    """Get access token for backend API using managed identity"""
    if not credential:
        return None

    try:
        token: AccessToken = await asyncio.to_thread(
            credential.get_token,
            f"{BACKEND_CLIENT_ID}/.default"
        )
        return token.token
    except Exception as e:
        logger.error(f"Failed to get access token: {e}")
        return None


@app.get("/")
async def root():
    return {
        "status": "healthy",
        "service": "azure-sdk-qa-bot-mcp",
        "version": "1.0.0"
    }


@app.get("/health")
async def health_check():
    return {
        "status": "healthy",
        "service": "azure-sdk-qa-bot-mcp",
        "code_review_url": CODE_REVIEW_API_URL,
        "completion_url": COMPLETION_API_URL
    }


@app.get("/robots933456.txt")
async def robots():
    return "OK"


async def execute_code_review(arguments: dict) -> str:
    language = arguments.get("language")
    code = arguments.get("code")

    if not language:
        return "Error: 'language' is required"
    if not code:
        return "Error: 'code' is required"

    payload = {"language": language, "code": code}
    file_path = arguments.get("file_path")
    if file_path:
        payload["file_path"] = file_path

    backend_token = await get_backend_token()

    headers = {"Content-Type": "application/json"}
    if backend_token:
        headers["Authorization"] = f"Bearer {backend_token}"
        logger.info("Making authenticated request to backend")
    else:
        logger.warning("No backend token available, making unauthenticated request")

    try:
        response = await http_client.post(
            CODE_REVIEW_API_URL,
            json=payload,
            headers=headers
        )
        response.raise_for_status()
        result = response.json()
    except httpx.TimeoutException:
        return "Error: Request timed out. The code review API may be slow or unavailable."
    except httpx.HTTPStatusError as e:
        return f"Error: HTTP {e.response.status_code} - {e.response.text}"
    except httpx.RequestError as e:
        return f"Error: Failed to connect to code review API: {str(e)}"

    return format_review_result(result)


async def execute_typespec_question(arguments: dict) -> str:
    """Execute TypeSpec question completion."""
    message = arguments.get("message")

    if not message:
        return "Error: 'message' is required"

    history = arguments.get("history", [])
    additional_context = arguments.get("additional_context", [])

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

    backend_token = await get_backend_token()

    headers = {"Content-Type": "application/json"}
    if backend_token:
        headers["Authorization"] = f"Bearer {backend_token}"
        logger.info("Making authenticated request to completion API")
    else:
        logger.warning("No backend token available, making unauthenticated request")

    try:
        response = await http_client.post(
            COMPLETION_API_URL,
            json=payload,
            headers=headers,
            timeout=180.0
        )
        response.raise_for_status()
        result = response.json()
    except httpx.TimeoutException:
        return "Error: Request timed out. The completion API may be slow or unavailable."
    except httpx.HTTPStatusError as e:
        return f"Error: HTTP {e.response.status_code} - {e.response.text}"
    except httpx.RequestError as e:
        return f"Error: Failed to connect to completion API: {str(e)}"

    return format_completion_result(result)


@app.get("/sse")
async def sse_stream(request: Request):
    session_id = str(uuid.uuid4())
    queue: asyncio.Queue = asyncio.Queue()
    message_queues[session_id] = queue

    client_host = request.client.host if request.client else "unknown"
    endpoint_url = _build_external_url(f"/sse/messages/{session_id}", request)

    logger.info(
        f"SSE GET connection established from {client_host}, session={session_id}, "
        f"query={dict(request.query_params)}, headers={_interesting_headers(request.headers)}, "
        f"endpoint={endpoint_url}"
    )

    server_info = {
        "jsonrpc": "2.0",
        "method": "server/initialized",
        "params": {
            "protocolVersion": "2024-11-05",
            "capabilities": {"tools": {}},
            "serverInfo": {
                "name": "azure-sdk-qa-bot",
                "version": "1.0.0"
            }
        }
    }
    queue.put_nowait(server_info)

    async def event_generator():
        try:
            yield "event: endpoint\n"
            yield f"data: {endpoint_url}\n\n"
            logger.info(f"Sent endpoint event for session {session_id}")

            while True:
                try:
                    message = await asyncio.wait_for(queue.get(), timeout=15)
                    yield "event: message\n"
                    yield f"data: {json.dumps(message)}\n\n"
                except asyncio.TimeoutError:
                    yield ": keepalive\n\n"
        except asyncio.CancelledError:
            logger.info(f"SSE connection closed for session {session_id}")
        except Exception as e:
            logger.error(f"Error in SSE stream for session {session_id}: {e}")
        finally:
            message_queues.pop(session_id, None)
            logger.info(f"Cleaned up session {session_id}")

    return StreamingResponse(
        event_generator(),
        media_type="text/event-stream",
        headers={
            "Cache-Control": "no-cache",
            "Connection": "keep-alive",
            "X-Accel-Buffering": "no",
            "Access-Control-Allow-Origin": "*"
        }
    )


@app.post("/sse/messages/{session_id}")
async def sse_message_endpoint(session_id: str, request: Request):
    queue = message_queues.get(session_id)
    if not queue:
        logger.warning(f"Received POST for unknown session {session_id}")
        raise HTTPException(status_code=404, detail="Unknown SSE session")

    client_host = request.client.host if request.client else "unknown"
    logger.info(
        f"Received POST for session {session_id} from {client_host}, "
        f"headers={_interesting_headers(request.headers)}"
    )

    try:
        body = await request.json()
    except Exception as exc:
        logger.error(f"Invalid JSON payload for session {session_id}: {exc}")
        raise HTTPException(status_code=400, detail="Invalid JSON payload") from exc

    response = await process_mcp_message(body)
    if response is not None:
        await queue.put(response)
    return {"status": "ok"}


@app.post("/sse")
async def legacy_sse_post(request: Request):
    client_host = request.client.host if request.client else "unknown"
    logger.info(
        f"Received POST request to /sse from {client_host}, "
        f"headers={_interesting_headers(request.headers)}"
    )
    return await mcp_messages_direct(request)


async def process_mcp_message(body: dict) -> Optional[dict]:
    method = body.get("method")
    logger.info(f"Processing MCP message: {method}")

    if method == "initialize":
        return {
            "jsonrpc": "2.0",
            "id": body.get("id"),
            "result": {
                "protocolVersion": "2024-11-05",
                "capabilities": {"tools": {}},
                "serverInfo": {
                    "name": "azure-sdk-qa-bot",
                    "version": "1.0.0"
                }
            }
        }

    if method in {"notifications/initialized", "initialized"}:
        logger.info("Client initialized successfully")
        return None

    if method == "tools/list":
        return await get_tools_list_response(body.get("id"))

    if method == "tools/call":
        params = body.get("params", {})
        tool_name = params.get("name")
        arguments = params.get("arguments", {})

        if tool_name == "review_sdk_code":
            result = await execute_code_review(arguments)
        elif tool_name == "ask_typespec_question":
            result = await execute_typespec_question(arguments)
        else:
            return {
                "jsonrpc": "2.0",
                "id": body.get("id"),
                "error": {
                    "code": -32602,
                    "message": f"Unknown tool: {tool_name}"
                }
            }

        return {
            "jsonrpc": "2.0",
            "id": body.get("id"),
            "result": {
                "content": [
                    {"type": "text", "text": result}
                ]
            }
        }

    return {
        "jsonrpc": "2.0",
        "id": body.get("id"),
        "error": {
            "code": -32601,
            "message": f"Method not found: {method}"
        }
    }


@app.post("/messages")
async def mcp_messages_direct(request: Request):
    try:
        body = await request.json()
        response = await process_mcp_message(body)
        if response is None:
            return JSONResponse(content=None, status_code=200)
        return response
    except Exception as e:
        logger.error(f"Error handling MCP message: {e}", exc_info=True)
        return {
            "jsonrpc": "2.0",
            "id": body.get("id") if "body" in locals() else None,
            "error": {
                "code": -32603,
                "message": str(e)
            }
        }


async def get_tools_list_response(request_id):
    return {
        "jsonrpc": "2.0",
        "id": request_id,
        "result": {
            "tools": [
                {
                    "name": "review_sdk_code",
                    "description": """Review SDK code against Azure SDK guidelines.

This tool analyzes code to check if it follows Azure SDK design guidelines for the specified language.
It returns comments about potential guideline violations with suggestions for fixes.

Supported languages: go, python, java, javascript, dotnet""",
                    "inputSchema": {
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
                },
                {
                    "name": "ask_typespec_question",
                    "description": """Ask questions about TypeSpec for Azure API specifications.

This tool provides answers to questions about:
- TypeSpec language syntax and features
- TypeSpec decorators and built-in types
- Azure TypeSpec libraries (@azure-tools/typespec-azure-core, @azure-tools/typespec-azure-resource-manager, etc.)
- API specification authoring with TypeSpec
- TypeSpec project configuration and setup
- Migrating from Swagger/OpenAPI to TypeSpec

The tool uses RAG (Retrieval-Augmented Generation) to provide accurate answers based on official TypeSpec documentation.""",
                    "inputSchema": {
                        "type": "object",
                        "properties": {
                            "message": {
                                "type": "string",
                                "description": "The question to ask about TypeSpec"
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
                                "description": "Optional: Additional context such as links, images, or text",
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
                }
            ]
        }
    }


def format_review_result(result: dict) -> str:
    output = ["# Code Review Results"]
    output.append(f"**Review ID:** {result.get('id', 'N/A')}")
    output.append(f"**Language:** {result.get('language', 'N/A')}")
    output.append(f"**Summary:** {result.get('summary', 'No summary available')}")
    output.append("")

    comments = result.get("comments", [])
    if not comments:
        output.append("✅ No issues found. Code looks good!")
        return "\n".join(output)

    output.append(f"## Found {len(comments)} Issue(s):")
    output.append("")

    for idx, comment in enumerate(comments, 1):
        output.append(f"### Issue {idx}")
        output.append("")

        if comment.get("line_number"):
            output.append(f"**Line:** {comment['line_number']}")
            output.append("")

        if comment.get("comment"):
            output.append(f"**Issue:** {comment['comment']}")
            output.append("")

        if comment.get("bad_code"):
            output.append("**Bad Code:**")
            output.append("```")
            output.append(comment["bad_code"])
            output.append("```")
            output.append("")

        if comment.get("suggestion"):
            output.append("**Suggested Fix:**")
            output.append("```")
            output.append(comment["suggestion"])
            output.append("```")
            output.append("")

        if comment.get("guideline_content"):
            output.append(f"**Guideline:** {comment['guideline_content']}")
            output.append("")

        if comment.get("guideline_link"):
            label = comment.get("guideline_id", "Documentation")
            output.append(f"**Reference:** [{label}]({comment['guideline_link']})")
            output.append("")

        if idx < len(comments):
            output.append("---")
            output.append("")

    return "\n".join(output)


def format_completion_result(result: dict) -> str:
    """Format the completion result into a readable string."""
    output = []
    
    answer = result.get("answer", "")
    has_result = result.get("has_result", False)
    references = result.get("references", [])

    if not has_result:
        output.append("I couldn't find a specific answer to your question.")
        output.append("")
    
    if answer:
        output.append(answer)
        output.append("")

    if references:
        output.append("## References")
        output.append("")
        for ref in references:
            title = ref.get("title", "Untitled")
            link = ref.get("link", "")
            if link:
                output.append(f"- [{title}]({link})")
            else:
                output.append(f"- {title}")
        output.append("")

    return "\n".join(output)


if __name__ == "__main__":
    import uvicorn

    logger.info(f"Starting server on port {PORT}")
    uvicorn.run(
        app,
        host="0.0.0.0",
        port=PORT,
        log_level="info"
    )
