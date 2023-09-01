import os
import json

from ._version import VERSION
from ._gpt_reviewer import GptReviewer
from ._python_api import review_python
from ._vector_db import VectorDB
from ._models import GuidelinesResult, Violation, VectorDocument, VectorSearchResult

__version__ = VERSION

_PACKAGE_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
