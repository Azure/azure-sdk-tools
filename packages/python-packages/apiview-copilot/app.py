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
    content = data["content"]
    result = ApiViewReview(language=language, model="gpt-4.1-nano").get_response(
        content
    )
    return jsonify(result.model_dump_json())
