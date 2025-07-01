import os

from ._version import VERSION
from ._apiview_reviewer import ApiViewReview
from ._models import ReviewResult, Comment, ExistingComment

__version__ = VERSION

_PACKAGE_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
