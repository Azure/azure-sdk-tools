#!/usr/bin/env python3
"""
HTTP-based MCP Server for Azure SDK Code Review API

This server exposes the MCP protocol over HTTP/SSE for deployment on Azure App Service.
It supports Microsoft Entra ID authentication and forwards requests to the backend API.
"""

import os
import json
import asyncio
import logging
from typing import Optional
from contextlib import asynccontextmanager

import httpx
from fastapi import FastAPI, HTTPException, Header, Request
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
CODE_REVIEW_API_URL = os.getenv("CODE_REVIEW_API_URL", "https://azuresdkqabot-dev-server-hrcrckaad5gcedcv.westus2-01.azurewebsites.net/code_review")
BACKEND_CLIENT_ID = os.getenv("BACKEND_CLIENT_ID", "api://azure-sdk-qa-bot-dev")
AZURE_CLIENT_ID = os.getenv("AZURE_CLIENT_ID")  # User-assigned managed identity client ID
PORT = int(os.getenv("PORT", "8000"))

# Global HTTP client
http_client: Optional[httpx.AsyncClient] = None
credential: Optional[DefaultAzureCredential] = None


@asynccontextmanager
async def lifespan(app: FastAPI):
    """Manage application lifecycle"""
    global http_client, credential
    
    # Startup
    logger.info("Starting Code Review MCP Server")
    logger.info(f"Listening on port: {PORT}")
    logger.info(f"Backend API URL: {CODE_REVIEW_API_URL}")
    logger.info(f"Backend Client ID: {BACKEND_CLIENT_ID}")
    if AZURE_CLIENT_ID:
        logger.info(f"User-Assigned Managed Identity Client ID: {AZURE_CLIENT_ID}")
    
    http_client = httpx.AsyncClient(timeout=120.0)
    
    # Initialize Azure credential (Managed Identity in Azure, DefaultAzureCredential for local dev)
    try:
        if os.getenv("WEBSITE_INSTANCE_ID"):  # Running in Azure App Service
            if AZURE_CLIENT_ID:
                # Use user-assigned managed identity with explicit client ID
                credential = ManagedIdentityCredential(client_id=AZURE_CLIENT_ID)
                logger.info("Using User-Assigned Managed Identity for authentication")
            else:
                # Use system-assigned managed identity
                credential = ManagedIdentityCredential()
                logger.info("Using System-Assigned Managed Identity for authentication")
        else:
            credential = DefaultAzureCredential()
            logger.info("Using DefaultAzureCredential for authentication")
    except Exception as e:
        logger.warning(f"Failed to initialize Azure credential: {e}")
        credential = None
    
    yield
    
    # Shutdown
    logger.info("Shutting down Code Review MCP Server")
    if http_client:
        await http_client.aclose()


app = FastAPI(
    title="Azure SDK Code Review MCP Server",
    description="MCP Server for reviewing SDK code against Azure guidelines",
    version="1.0.0",
    lifespan=lifespan
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
    """Root endpoint for Azure App Service health checks"""
    return {
        "status": "healthy",
        "service": "code-review-mcp",
        "version": "1.0.0"
    }


@app.get("/health")
async def health_check():
    """Health check endpoint"""
    return {
        "status": "healthy",
        "service": "code-review-mcp",
        "backend_url": CODE_REVIEW_API_URL
    }


@app.get("/robots933456.txt")
async def robots():
    """Azure App Service warmup probe"""
    return "OK"


@app.post("/messages")
async def mcp_messages(request: Request):
    """MCP protocol messages endpoint - handles initialize and other protocol messages"""
    try:
        body = await request.json()
        method = body.get("method")
        
        if method == "initialize":
            # Respond to initialize request
            return {
                "jsonrpc": "2.0",
                "id": body.get("id"),
                "result": {
                    "protocolVersion": "2024-11-05",
                    "capabilities": {
                        "tools": {}
                    },
                    "serverInfo": {
                        "name": "azure-sdk-code-review",
                        "version": "1.0.0"
                    }
                }
            }
        
        elif method == "tools/list":
            # Return available tools
            return {
                "jsonrpc": "2.0",
                "id": body.get("id"),
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
                        }
                    ]
                }
            }
        
        elif method == "tools/call":
            # Execute a tool
            params = body.get("params", {})
            tool_name = params.get("name")
            arguments = params.get("arguments", {})
            
            if tool_name != "review_sdk_code":
                return {
                    "jsonrpc": "2.0",
                    "id": body.get("id"),
                    "error": {
                        "code": -32602,
                        "message": f"Unknown tool: {tool_name}"
                    }
                }
            
            # Execute the code review
            result = await execute_code_review(arguments)
            
            return {
                "jsonrpc": "2.0",
                "id": body.get("id"),
                "result": {
                    "content": [
                        {
                            "type": "text",
                            "text": result
                        }
                    ]
                }
            }
        
        else:
            return {
                "jsonrpc": "2.0",
                "id": body.get("id"),
                "error": {
                    "code": -32601,
                    "message": f"Method not found: {method}"
                }
            }
    
    except Exception as e:
        logger.error(f"Error handling MCP message: {e}", exc_info=True)
        return {
            "jsonrpc": "2.0",
            "id": body.get("id") if "id" in locals() else None,
            "error": {
                "code": -32603,
                "message": str(e)
            }
        }


async def execute_code_review(arguments: dict) -> str:
    """Execute code review and return formatted result"""
    # Validate required arguments
    language = arguments.get("language")
    code = arguments.get("code")
    
    if not language:
        return "Error: 'language' is required"
    if not code:
        return "Error: 'code' is required"
    
    # Prepare request payload for backend
    payload = {
        "language": language,
        "code": code
    }
    
    file_path = arguments.get("file_path")
    if file_path:
        payload["file_path"] = file_path
    
    # Get access token for backend
    backend_token = await get_backend_token()
    
    headers = {"Content-Type": "application/json"}
    if backend_token:
        headers["Authorization"] = f"Bearer {backend_token}"
        logger.info("Making authenticated request to backend")
    else:
        logger.warning("No backend token available, making unauthenticated request")
    
    # Call backend API
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
    
    # Format response
    return format_review_result(result)


@app.api_route("/sse", methods=["GET", "POST"])
async def sse_endpoint(request: Request):
    """Combined SSE and JSON-RPC endpoint for MCP protocol"""
    if request.method == "GET":
        # SSE stream for notifications
        async def event_generator():
            # Keep connection alive with periodic pings
            try:
                while True:
                    await asyncio.sleep(30)
                    yield ": ping\n\n"
            except asyncio.CancelledError:
                logger.info("SSE connection closed")
        
        return StreamingResponse(
            event_generator(),
            media_type="text/event-stream",
            headers={
                "Cache-Control": "no-cache",
                "Connection": "keep-alive",
                "X-Accel-Buffering": "no"
            }
        )
    
    elif request.method == "POST":
        # Handle JSON-RPC messages via POST to same endpoint
        return await mcp_messages(request)


def format_review_result(result: dict) -> str:
    """Format the review result into human-readable text"""
    output = []
    
    # Header
    output.append("# Code Review Results")
    output.append(f"**Review ID:** {result.get('id', 'N/A')}")
    output.append(f"**Language:** {result.get('language', 'N/A')}")
    output.append(f"**Summary:** {result.get('summary', 'No summary available')}")
    output.append("")
    
    comments = result.get("comments", [])
    
    if not comments:
        output.append("✅ No issues found. Code looks good!")
        return "\n".join(output)
    
    # Comments
    output.append(f"## Found {len(comments)} Issue(s):")
    output.append("")
    
    for i, comment in enumerate(comments, 1):
        output.append(f"### Issue {i}")
        output.append("")
        
        # Line number if available
        if comment.get("line_number"):
            output.append(f"**Line:** {comment['line_number']}")
            output.append("")
        
        # Comment/description of the issue
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
        
        # Guideline content if available
        if comment.get("guideline_content"):
            output.append(f"**Guideline:** {comment['guideline_content']}")
            output.append("")
        
        if comment.get("guideline_link"):
            guideline_text = comment.get('guideline_id', 'Documentation')
            output.append(f"**Reference:** [{guideline_text}]({comment['guideline_link']})")
            output.append("")
        
        if i < len(comments):
            output.append("---")
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
