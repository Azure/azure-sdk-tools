import dotenv
import json
import os
import pprint
import requests
import sys
import traceback

dotenv.load_dotenv()

SERVICE_URL = os.getenv("APIVIEW_GPT_SERVICE_URL")

if __name__ == "__main__":
    try:
        # TODO: Make this generic via command line arguments
        input_filename = "test2.txt"
        language = "python"
        file_path = os.path.join(os.path.dirname(__file__), input_filename)
        with open(file_path, "r") as f:
            apiview_text = f.read()
        request_body = {
            "content": apiview_text,
        }
        response = requests.post(f"{SERVICE_URL}/{language}", json=request_body)
        response.raise_for_status()
        result = json.loads(response.json())
        pprint.pprint(result)
        sys.exit(0)
    except Exception as err:
        exc_type, exc_val, exc_tb = sys.exc_info()
        traceback.print_exception(exc_type, exc_val, exc_tb, file=sys.stderr)
        sys.exit(1)
