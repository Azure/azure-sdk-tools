import uuid
import threading
from fastapi import BackgroundTasks

from src._apiview_reviewer import ApiViewReview
from fastapi import FastAPI, HTTPException, Request
from fastapi.responses import JSONResponse
import json
import logging
import os

app = FastAPI()
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

# In-memory job store (for demo/prototype; use persistent store for production)
job_store = {}


# This is the legacy endpoint for API review
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


def api_review_job(job_id, language, target_apiview, base_apiview, outline, comments):
    try:
        reviewer = ApiViewReview(
            language=language, target=target_apiview, base=base_apiview, outline=outline, comments=comments
        )
        result = reviewer.run()
        reviewer.close()
        job_store[job_id]["status"] = "completed"
        job_store[job_id]["result"] = json.loads(result.model_dump_json())
    except Exception as e:
        logger.error(f"Error in background review job {job_id}: {str(e)}", exc_info=True)
        job_store[job_id]["status"] = "failed"
        job_store[job_id]["error"] = str(e)


@app.post("/api-review")
async def begin_api_review(request: Request, background_tasks: BackgroundTasks):
    """
    Start a new API review job.
    """
    data = await request.json()
    language = data.get("language")
    target_apiview = data.get("target")
    base_apiview = data.get("base")
    outline = data.get("outline")
    comments = data.get("comments")

    if not language or not target_apiview:
        raise HTTPException(status_code=400, detail="Missing required parameters: language and target")
    if language not in supported_languages:
        raise HTTPException(status_code=400, detail="Unsupported language")

    job_id = str(uuid.uuid4())
    job_store[job_id] = {"status": "pending", "result": None, "error": None}
    # Start background job
    thread = threading.Thread(
        target=api_review_job,
        args=(job_id, language, target_apiview, base_apiview, outline, comments),
        daemon=True,
    )
    thread.start()
    return JSONResponse(status_code=202, content={"job_id": job_id, "status": "pending"})


@app.get("/api-review/{job_id}")
async def get_api_review_status(job_id: str):
    """
    Get the status of an API review job.
    """
    job = job_store.get(job_id)
    if not job:
        raise HTTPException(status_code=404, detail="Job ID not found")
    response = {"job_id": job_id, "status": job["status"]}
    if job["status"] == "completed":
        response["result"] = job["result"]
    elif job["status"] == "failed":
        response["error"] = job["error"]
    return JSONResponse(content=response)
