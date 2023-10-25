from flask import Flask, request, jsonify
from src import (
    review_python,
    review_java,
    review_cpp,
    review_golang,
    review_typescript,
    review_dotnet,
    review_clang,
    review_ios,
    review_rest,
    review_android,
)

app = Flask(__name__)

@app.route('/python', methods=['POST'])
def python_api_reviewer():
    data = request.get_json()
    content = data['content']
    result = review_python(content)
    return jsonify(result)

@app.route('/java', methods=['POST'])
def java_api_reviewer():
    data = request.get_json()
    content = data['content']
    result = review_java(content)
    return jsonify(result)

@app.route('/typescript', methods=['POST'])
def typescript_api_reviewer():
    data = request.get_json()
    content = data['content']
    result = review_typescript(content)
    return jsonify(result)

@app.route('/dotnet', methods=['POST'])
def dotnet_api_reviewer():
    data = request.get_json()
    content = data['content']
    result = review_dotnet(content)
    return jsonify(result)

@app.route('/cpp', methods=['POST'])
def cpp_api_reviewer():
    data = request.get_json()
    content = data['content']
    result = review_cpp(content)
    return jsonify(result)

@app.route('/golang', methods=['POST'])
def golang_api_reviewer():
    data = request.get_json()
    content = data['content']
    result = review_golang(content)
    return jsonify(result)

@app.route('/clang', methods=['POST'])
def clang_api_reviewer():
    data = request.get_json()
    content = data['content']
    result = review_clang(content)
    return jsonify(result)

@app.route('/ios', methods=['POST'])
def ios_api_reviewer():
    data = request.get_json()
    content = data['content']
    result = review_ios(content)
    return jsonify(result)

@app.route('/rest', methods=['POST'])
def rest_api_reviewer():
    data = request.get_json()
    content = data['content']
    result = review_rest(content)
    return jsonify(result)

@app.route('/android', methods=['POST'])
def android_api_reviewer():
    data = request.get_json()
    content = data['content']
    result = review_android(content)
    return jsonify(result)
