from src import ApiViewReview
from flask import Flask, request, jsonify

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


@app.route("/<language>", methods=["POST"])
def api_reviewer(language: str):
    if language not in supported_languages:
        return jsonify({"error": "Unsupported language"}), 400
    data = request.get_json()
    # check for the new key "target"; otherwise fall back to the old value "content"
    target_apiview = data.get("target", data.get("content", None))
    base_apiview = data.get("base", None)
    apiview_diff = data.get("diff", None)
    result = ApiViewReview(language=language).get_response(target=target_apiview, base=base_apiview, diff=apiview_diff)
    return jsonify(result.model_dump_json())
