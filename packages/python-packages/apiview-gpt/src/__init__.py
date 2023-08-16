import os
import json

from ._version import VERSION
from ._gpt_reviewer import GptReviewer
from ._python_api import review_python

__version__ = VERSION

_PACKAGE_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))

def console_entry_point():
    print("Running apiview-gpt version {}".format(__version__))
    reviewer = GptReviewer()
    # FIXME: Make this a proper CLI
    input_filename = "test2.txt"
    file_path = os.path.join(_PACKAGE_ROOT, input_filename)
    with open(file_path, "r") as f:
        apiview_text = f.read()
    result = reviewer.get_response(apiview_text, "python")
    output_path = os.path.join(_PACKAGE_ROOT, "output.json")
    with open(output_path, "w") as f:
        f.write(result.json(indent=2))
