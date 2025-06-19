from fastapi import FastAPI, HTTPException, Request
from fastapi.responses import JSONResponse
from src._apiview_reviewer import ApiViewReview
import json
import logging
import os
from fastapi import FastAPI
from src.agent._api import router as agent_router
from fastapi import APIRouter
from pydantic import BaseModel
from src.agent._agent import get_main_agent
import asyncio
from semantic_kernel.agents import AzureAIAgentThread
import uuid
from semantic_kernel.exceptions.agent_exceptions import AgentInvokeException

app = FastAPI()
app.include_router(agent_router)

logger = logging.getLogger("uvicorn")  # Use Uvicorn's logger

supported_languages = [
    "android",
    "clang",
    "cpp",
    "dotnet",
    "golang",
    "ios",
    "java",
    "python",
    "rest",
    "typescript",
]

_PACKAGE_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__)))
error_log_file = os.path.join(_PACKAGE_ROOT, "error.log")


class AgentChatRequest(BaseModel):
    user_input: str
    thread_id: str = None
    messages: list = None  # Optional: for multi-turn


class AgentChatResponse(BaseModel):
    response: str
    thread_id: str
    messages: list


@app.post("/{language}")
async def api_reviewer(language: str, request: Request):
    logger.info(f"Received request for language: {language}")
    logging.getLogger("uvicorn.error").debug(f"Request body: {await request.body()}")

    if language not in supported_languages:
        logger.warning(f"Unsupported language: {language}")
        raise HTTPException(status_code=400, detail="Unsupported language")

    try:
        data = await request.json()

        target_apiview = data.get("target", None)
        target_id = data.get("target_id", None)
        base_apiview = data.get("base", None)
        outline = data.get("outline", None)
        comments = data.get("comments", None)

        if not target_apiview:
            logger.warning("No API content provided in the request")
            raise HTTPException(status_code=400, detail="No API content provided")

        logger.info(f"Processing {language} API review")

        reviewer = ApiViewReview(
            language=language, target=target_apiview, base=base_apiview, outline=outline, comments=comments
        )
        result = reviewer.run()
        reviewer.close()

        # Check if "error.log" file exists and is not empty
        if os.path.exists(error_log_file) and os.path.getsize(error_log_file) > 0:
            with open(error_log_file, "r") as f:
                error_message = f.read()
                logger.error(f"Error log contents:\n{error_message}")

        logger.info("API review completed successfully")

        # TODO: Add logic to post comments to the target_id, if provided

        return JSONResponse(content=json.loads(result.model_dump_json()))

    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Error processing request: {str(e)}", exc_info=True)
        raise HTTPException(status_code=500, detail="Internal server error")


@app.post("/agent/chat", response_model=AgentChatResponse)
async def agent_chat_thread_endpoint(request: AgentChatRequest):
    user_input = request.user_input
    thread_id = request.thread_id
    messages = request.messages or []
    # Only append user_input if not already the last message
    if not messages or messages[-1] != user_input:
        messages.append(user_input)
    try:
        async with get_main_agent() as agent:
            # Only use thread_id if it is a valid Azure thread id (starts with 'thread')
            thread = None
            if thread_id and isinstance(thread_id, str) and thread_id.startswith("thread"):
                thread = AzureAIAgentThread(client=agent.client, thread_id=thread_id)
            else:
                thread = AzureAIAgentThread(client=agent.client)
            response = await agent.get_response(messages=messages, thread=thread)
            # Get the thread id from the thread object if available
            thread_id_out = getattr(thread, "id", None) or thread_id
        return AgentChatResponse(response=str(response), thread_id=thread_id_out, messages=messages)
    except AgentInvokeException as e:
        if "Rate limit is exceeded" in str(e):
            logger.warning(f"Rate limit exceeded: {e}")
            raise HTTPException(status_code=429, detail="Rate limit exceeded. Please wait and try again.")
        logger.error(f"AgentInvokeException: {e}")
        raise HTTPException(status_code=500, detail="Agent error: " + str(e))
    except Exception as e:
        logger.error(f"Error in /agent/chat: {e}", exc_info=True)
        raise HTTPException(status_code=500, detail="Internal server error")
