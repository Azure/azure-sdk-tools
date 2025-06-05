from fastapi import APIRouter, Request
from fastapi.responses import JSONResponse
from src.agent._planner import AgentReviewPlanner

router = APIRouter()


@router.post("/agent/review")
async def agent_review(request: Request):
    data = await request.json()

    planner = AgentReviewPlanner(
        target=data.get("target"),
        base=data.get("base"),
        language=data.get("language"),
        comments=data.get("comments"),
    )

    try:
        result = planner.run()
    except Exception as e:
        return JSONResponse(content={"error": str(e)}, status_code=400)

    return JSONResponse(content={"result": result})
