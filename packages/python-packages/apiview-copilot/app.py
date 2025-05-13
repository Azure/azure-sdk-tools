from src import ApiViewReview
from flask import Flask, request, jsonify
import os
import json
import logging

app = Flask(__name__)

# Configure logging
logging.basicConfig(
    level=logging.INFO,  # Set to INFO for general logs
    format="%(asctime)s - %(levelname)s - %(message)s",
    handlers=[logging.FileHandler("app.log"), logging.StreamHandler()],  # Log to a file  # Log to the console
)
logger = logging.getLogger(__name__)

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


@app.route("/<language>", methods=["POST"])
def api_reviewer(language: str):
    logger.info(f"Received request for language: {language}")

    if language not in supported_languages:
        logger.warning(f"Unsupported language: {language}")
        return jsonify({"error": "Unsupported language"}), 400

    try:
        data = request.get_json()

        target_apiview = data.get("target", None)
        base_apiview = data.get("base", None)

        if not target_apiview:
            logger.warning("No API content provided in the request")
            return jsonify({"error": "No API content provided"}), 400

        logger.info(f"Processing {language} API review")

        # Create reviewer and get response
        reviewer = ApiViewReview(language=language, target=target_apiview, base=base_apiview)
        result = reviewer.run()
        reviewer.close()

        # Check if "error.log" file exists and is not empty
        if os.path.exists(error_log_file) and os.path.getsize(error_log_file) > 0:
            with open(error_log_file, "r") as f:
                error_message = f.read()
                logger.error(f"Error log contents:\n{error_message}")

        logger.info("API review completed successfully")
        return jsonify(json.loads(result.model_dump_json()))

    except Exception as e:
        logger.error(f"Error processing request: {str(e)}", exc_info=True)
        return jsonify({"error": "Internal server error"}), 500
