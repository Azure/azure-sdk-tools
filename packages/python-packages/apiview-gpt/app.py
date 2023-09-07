from flask import Flask, request, jsonify
from src import review_python, review_java, review_cpp, review_go, review_js, review_net

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

@app.route('/js', methods=['POST'])
def js_api_reviewer():
    data = request.get_json()
    content = data['content']
    result = review_js(content)
    return jsonify(result)

@app.route('/net', methods=['POST'])
def net_api_reviewer():
    data = request.get_json()
    content = data['content']
    result = review_net(content)
    return jsonify(result)

@app.route('/cpp', methods=['POST'])
def cpp_api_reviewer():
    data = request.get_json()
    content = data['content']
    result = review_cpp(content)
    return jsonify(result)

@app.route('/go', methods=['POST'])
def go_api_reviewer():
    data = request.get_json()
    content = data['content']
    result = review_go(content)
    return jsonify(result)
