# test_disallowed_import_from
from azure.core.pipeline.transport import HttpResponse

# test_allowed_imports
from math import PI
from azure.core.pipeline import Pipeline
from .. import HttpResponse