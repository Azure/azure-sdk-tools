from src import ApiViewReview
from flask import Flask, request, jsonify

app = Flask(__name__)

def _review(*, code: str, language: str):
  reviewer = ApiViewReview(language=language)
  result = reviewer.get_response(code)
  return result.model_dump_json()


@app.route('/python', methods=['POST'])
def python_api_reviewer():
    data = request.get_json()
    content = data['content']
    result = _review(code=content, language="python")
    return jsonify(result)

@app.route('/java', methods=['POST'])
def java_api_reviewer():
    data = request.get_json()
    content = data['content']
    result = _review(code=content, language="java")
    return jsonify(result)

@app.route('/typescript', methods=['POST'])
def typescript_api_reviewer():
    data = request.get_json()
    content = data['content']
    result = _review(code=content, language="typescript")
    return jsonify(result)

@app.route('/dotnet', methods=['POST'])
def dotnet_api_reviewer():
    data = request.get_json()
    content = data['content']
    result = _review(code=content, language="dotnet")
    return jsonify(result)

@app.route('/cpp', methods=['POST'])
def cpp_api_reviewer():
    data = request.get_json()
    content = data['content']
    result = _review(code=content, language="cpp")
    return jsonify(result)

@app.route('/golang', methods=['POST'])
def golang_api_reviewer():
    data = request.get_json()
    content = data['content']
    result = _review(code=content, language="golang")
    return jsonify(result)

@app.route('/clang', methods=['POST'])
def clang_api_reviewer():
    data = request.get_json()
    content = data['content']
    result = _review(code=content, language="clang")
    return jsonify(result)

@app.route('/ios', methods=['POST'])
def ios_api_reviewer():
    data = request.get_json()
    content = data['content']
    result = _review(code=content, language="ios")
    return jsonify(result)

@app.route('/rest', methods=['POST'])
def rest_api_reviewer():
    data = request.get_json()
    content = data['content']
    result = _review(code=content, language="rest")
    return jsonify(result)

@app.route('/android', methods=['POST'])
def android_api_reviewer():
    data = request.get_json()
    content = data['content']
    result = _review(code=content, language="android")
    return jsonify(result)
