from fastapi import APIRouter, Request
from fastapi.responses import JSONResponse

router = APIRouter()


@router.post("/agent/review")
async def agent_review(request: Request):
    data = await request.json()
    # Process the review here
    return JSONResponse(content={"message": "Review received", "data": data})
