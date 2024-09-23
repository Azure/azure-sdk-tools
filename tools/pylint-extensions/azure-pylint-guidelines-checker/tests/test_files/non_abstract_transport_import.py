# test_disallowed_imports
from azure.core.pipeline.transport import RequestsTransport

# test_allowed_imports
from math import PI
from azure.core.pipeline import Pipeline
from azure.core.pipeline.transport import HttpTransport, HttpRequest, HttpResponse, AsyncHttpTransport, AsyncHttpResponse
from azure.core.pipeline.transport import RequestsTransport, AioHttpTransport, AioHttpTransportResponse