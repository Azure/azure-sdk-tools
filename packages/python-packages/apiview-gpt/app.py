from flask import Flask, request, jsonify
from src import review_python

app = Flask(__name__)

@app.route('/')
def index():
    return 'Hello, World!'

@app.route('/python', methods=['POST'])
def python_api_reviewer():
    data = request.get_json()
    content = data['content']
    result = review_python(content)
    return jsonify(result)
