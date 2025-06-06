from fastapi import APIRouter, Request
from fastapi.responses import JSONResponse
from semantic_kernel.agents import ChatCompletionAgent

router = APIRouter()


@router.post("/agent/review")
async def agent_review(request: Request):
    from ._agent import get_review_agent

    data = await request.json()
    agent = get_review_agent()

    try:
        result = agent.run()
    except Exception as e:
        return JSONResponse(content={"error": str(e)}, status_code=400)

    return JSONResponse(content={"result": result})
