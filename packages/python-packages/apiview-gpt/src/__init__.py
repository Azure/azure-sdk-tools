import os
import json

from ._version import VERSION
from ._gpt_reviewer import GptReviewer

__version__ = VERSION

def console_entry_point():
    print("Running apiview-gpt version {}".format(__version__))
    reviewer = GptReviewer()
    # FIXME: Make this a proper CLI
    filename = "test.txt"
    file_path = os.path.join(os.path.dirname(__file__), "..", filename)
    with open(file_path, "r") as f:
        apiview_text = f.read()
    result = reviewer.get_response(apiview_text, "python")
    print(json.dumps(result, indent=2))
