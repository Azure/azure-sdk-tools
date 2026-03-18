"""Azure SDK QA Bot — Backend Server.

The backend server that the Teams App communicates with.
Calls the hosted Chat Agent via the Azure AI Foundry SDK (azure-ai-projects)
and handles feedback through a local workflow.
"""

import uvicorn
from contextlib import asynccontextmanager
from dotenv import load_dotenv

load_dotenv(override=False)

from fastapi import FastAPI

from models.chat import ChatRequest, ChatResponse
from models.conversation import ConversationMessage
from models.feedback import FeedbackRequest, FeedbackResponse
from services.chat_service import ChatService
from services.conversation_service import ConversationService
from services.feedback_service import FeedbackService
from utils.azure_ai_foundry import close_clients
from utils.azure_credential import close_credential
from pydantic import BaseModel


@asynccontextmanager
async def lifespan(application: FastAPI):
    """Startup / shutdown lifecycle for the FastAPI app."""
    yield
    # Cleanup SDK clients on shutdown
    await close_clients()
    await close_credential()


app = FastAPI(title="Azure SDK QA Bot Backend", lifespan=lifespan)

_chat_service = ChatService()
_conversation_service = ConversationService()
_feedback_service = FeedbackService()


@app.post("/agent/chat", response_model=ChatResponse)
async def handle_chat(req: ChatRequest):
    """Process a chat request through the chat service."""
    return await _chat_service.chat(req)


@app.post("/agent/feedback", response_model=FeedbackResponse)
async def handle_feedback(req: FeedbackRequest):
    """Process user feedback through the feedback workflow."""
    return await _feedback_service.process(req)


@app.post("/conversation/save",response_model=BaseModel)
async def save_conversation(req: ConversationMessage):
    """Save a conversation message."""
    return await _conversation_service.save_conversation(req)

if __name__ == "__main__":
    uvicorn.run(app, host="0.0.0.0", port=8080)