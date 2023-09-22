import os
import json

from ._version import VERSION
from ._gpt_reviewer import GptReviewer
from ._python_api import review_python
from ._java_api import review_java
from ._typescript_api import review_typescript
from ._dotnet_api import review_dotnet
from ._cpp_api import review_cpp
from ._golang_api import review_golang
from ._clang_api import review_clang
from ._ios_api import review_ios
from ._rest_api import review_rest
from ._android_api import review_android
from ._vector_db import VectorDB
from ._models import GuidelinesResult, Violation, VectorDocument, VectorSearchResult

__version__ = VERSION

_PACKAGE_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
