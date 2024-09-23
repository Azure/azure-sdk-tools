from flask import Flask

app = Flask(__name__)

html = ''
with (open('index.html', 'r')) as f:
    html = f.read()

@app.route("/")
def home():
    return f"{html}"

@app.route("/<path:path>", methods=['GET'])
def all_routes(path):
    return f"{html}"