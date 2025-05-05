from src import ApiViewReview
from flask import Flask, request, jsonify
import os
import json

app = Flask(__name__)

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
log_file = os.path.join(_PACKAGE_ROOT, "error.log")


@app.route("/<language>", methods=["POST"])
def api_reviewer(language: str):
    if language not in supported_languages:
        return jsonify({"error": "Unsupported language"}), 400

    try:
        data = request.get_json()
        # check for the new key "target"; otherwise fall back to the old value "content"
        target_apiview = data.get("target", data.get("content", None))
        base_apiview = data.get("base", None)

        if not target_apiview:
            return jsonify({"error": "No API content provided"}), 400

        # Log the request
        print(f"Processing {language} API review, content length: {len(target_apiview)}")

        # Create reviewer and get response
        reviewer = ApiViewReview(language=language, target=target_apiview, base=base_apiview)
        result = reviewer.run()
        reviewer.close()

        # check if "error.log" file exists and is not empty
        if os.path.exists(log_file) and os.path.getsize(log_file) > 0:
            with open(log_file, "r") as f:
                error_message = f.read()
                print(f"Error log:\n{error_message}")

        return jsonify(json.loads(result.model_dump_json()))

    except Exception as e:
        # Log the exception
        import traceback

        print(f"Error processing request: {str(e)}")
        print(traceback.format_exc())
        return jsonify({"error": f"Internal server error: {str(e)}"}), 500
