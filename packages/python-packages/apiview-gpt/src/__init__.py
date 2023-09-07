import os
import json

from ._version import VERSION
from ._gpt_reviewer import GptReviewer
from ._python_api import review_python
from ._java_api import review_java
from ._js_api import review_js
from ._net_api import review_net
from ._cpp_api import review_cpp
from ._go_api import review_go
from ._c_api import review_c
from ._swift_api import review_swift
from ._typespec_api import review_typespec
from ._vector_db import VectorDB
from ._models import GuidelinesResult, Violation, VectorDocument, VectorSearchResult

__version__ = VERSION

_PACKAGE_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
